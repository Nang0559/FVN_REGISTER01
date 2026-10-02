<##
  Apply-Patch.ps1 - the FVN_REGISTER updater. Runs every minute as a scheduled task (SYSTEM or a
  dedicated service account) and processes at most one signed package from <root>\patch-inbox.

  Flow: verify signature -> verify every file hash -> run SQL -> stage new release folders
        -> switch IIS physical path + restart app pool -> health check -> rollback on failure.

  The inbox is untrusted input (the web app writes to it), so nothing is executed or copied
  before the RSA signature and all file hashes have been verified.
#>
[CmdletBinding()]
param([string]$ConfigPath = (Join-Path $PSScriptRoot 'updater.config.json'))

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Updater.Common.ps1')
Initialize-Updater -ConfigPath $ConfigPath

function Invoke-PatchPackage {
    param([IO.FileInfo]$Package)
    $id = $Package.BaseName
    $metaPath = Join-Path $Package.DirectoryName "$id.meta.json"
    $uploader = 'unknown'
    if (Test-Path -LiteralPath $metaPath) { try { $uploader = [string](Get-Content -LiteralPath $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json).uploadedBy } catch { } }
    Start-Status -Id $id -Version '' -Uploader $uploader
    Add-Step "Nhận gói $($Package.Name) ($([math]::Round($Package.Length / 1MB, 1)) MB)"
    $work = Join-Path $script:WorkRoot $id
    $staged = New-Object System.Collections.ArrayList
    $switched = New-Object System.Collections.ArrayList
    $state = 'Failed'; $message = ''; $version = ''; $rollbackOk = $true
    try {
        Add-Step 'Giải nén gói'
        Expand-ZipSafe -Zip $Package.FullName -Dest $work -MaxBytes ([long]$script:Cfg.maxExtractMb * 1MB)
        $manifestPath = Join-Path $work 'manifest.json'; $sigPath = Join-Path $work 'manifest.sig'
        if (-not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $sigPath)) { throw (New-Rejection 'Package has no manifest.json / manifest.sig.') }
        if (-not (Test-Path -LiteralPath $script:KeyFile)) { throw (New-Rejection "Public key not found: $($script:KeyFile)") }
        Add-Step 'Xác thực chữ ký số'
        if (-not (Test-ManifestSignature -ManifestPath $manifestPath -SignaturePath $sigPath -PublicKeyXml (Get-Content -LiteralPath $script:KeyFile -Raw))) { throw (New-Rejection 'Invalid package signature.') }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $version = [string]$manifest.version; $script:St.version = $version; Save-Status
        Add-Step 'Kiểm tra hash từng file'; Assert-Manifest -Manifest $manifest -WorkDir $work
        $components = @(Get-ComponentNames | Where-Object { @($manifest.components) -contains $_ })
        foreach ($s in @($manifest.sql | Where-Object { $_ })) { Add-Step "Chạy SQL: $s"; Invoke-SqlScript -Path (Join-Path $work $s.Replace('/', '\')) }
        foreach ($comp in $components) {
            $c = Get-Component $comp; $cur = Get-SitePath -IisPath $c.iisPath
            Add-Step "Tạo bản phát hành $comp/$version"
            $dir = New-ReleaseDir -Comp $comp -Version $version -Mode $manifest.mode -CurrentDir $cur -PackageDir (Join-Path $work $comp) -DeleteList ($manifest.delete.$comp)
            [void]$staged.Add(@{ comp = $comp; dir = $dir; old = $cur })
        }
        foreach ($s in $staged) {
            $c = Get-Component $s.comp; $pool = Get-SitePool -IisPath $c.iisPath
            Add-Step "Chuyển $($s.comp) sang $($s.dir)"; Set-SitePath -IisPath $c.iisPath -Path $s.dir
            [void]$switched.Add(@{ comp = $s.comp; old = $s.old; pool = $pool; iis = $c.iisPath; url = $c.healthUrl })
            Restart-Pool -Pool $pool
            if (-not (Wait-Healthy -Url $c.healthUrl -TimeoutSec ([int]$script:Cfg.healthTimeoutSec))) { throw "Health check failed for $($s.comp): $($c.healthUrl)" }
            Add-Step "$($s.comp) hoạt động bình thường"
        }
        foreach ($comp in $components) {
            $protect = @($staged | Where-Object { $_.comp -eq $comp } | ForEach-Object { $_.dir.TrimEnd('\'); $_.old.TrimEnd('\') })
            Remove-OldReleases -Comp $comp -Protect $protect
        }
        $state = 'Succeeded'; $message = "Đã cập nhật lên phiên bản $version."
    }
    catch [System.Security.SecurityException] {
        $state = 'Rejected'; $message = $_.Exception.Message; Write-UpdLog $message 'ERROR'; Add-Step "Từ chối gói: $message"
    }
    catch {
        $message = $_.Exception.Message; Write-UpdLog $message 'ERROR'; Add-Step "Lỗi: $message"
        if ($switched.Count -gt 0) {
            Add-Step 'Rollback về phiên bản trước'; $rollbackOk = $true
            for ($i = $switched.Count - 1; $i -ge 0; $i--) {
                $w = $switched[$i]
                try { Set-SitePath -IisPath $w.iis -Path $w.old; Restart-Pool -Pool $w.pool; if (Wait-Healthy -Url $w.url -TimeoutSec 60) { Add-Step "Đã rollback $($w.comp)" } else { $rollbackOk = $false; Add-Step "Rollback $($w.comp) xong nhưng health check chưa đạt - cần kiểm tra thủ công" } }
                catch { $rollbackOk = $false; Write-UpdLog "Rollback of $($w.comp) failed: $($_.Exception.Message)" 'ERROR'; Add-Step "Rollback $($w.comp) thất bại: $($_.Exception.Message)" }
            }
            if ($rollbackOk) { $state = 'RolledBack' } else { $state = 'Failed'; $message = "$message | ROLLBACK KHÔNG HOÀN TẤT - cần kiểm tra thủ công ngay." }
        }
    }
    finally {
        if ($state -ne 'Succeeded') {
            foreach ($s in $staged) {
                $live = $false; try { $live = ((Get-SitePath -IisPath (Get-Component $s.comp).iisPath).TrimEnd('\') -ieq $s.dir.TrimEnd('\')) } catch { }
                if ($live) { Write-UpdLog "Keeping $($s.dir): still the active site path" 'WARN' } else { Remove-ReleaseDir -Dir $s.dir -SharedDirs @((Get-Component $s.comp).sharedDirs) }
            }
        }
        if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
        $target = if ($state -eq 'Succeeded') { $script:DoneDir } else { $script:RejectDir }
        Move-Item -LiteralPath $Package.FullName -Destination (Join-Path $target $Package.Name) -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $metaPath) { Move-Item -LiteralPath $metaPath -Destination (Join-Path $target "$id.meta.json") -Force -ErrorAction SilentlyContinue }
        Complete-Status -State $state -Message $message; Write-UpdLog "Package $id finished: $state"
    }
}

$mutex = New-Object System.Threading.Mutex($false, 'Global\FVN_REGISTER_Updater'); $owned = $false
try {
    $owned = $mutex.WaitOne(0)
    if ($owned) {
        $pkg = Get-ChildItem -LiteralPath $script:InboxDir -Filter 'patch-*.zip' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -First 1
        if ($pkg) { Invoke-PatchPackage -Package $pkg }
    }
}
finally { if ($owned) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
