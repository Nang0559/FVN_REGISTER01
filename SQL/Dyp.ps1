<#
  Deploy.ps1 - one-command deployment of FVN_REGISTER

  Examples (run from anywhere; the script switches to its own folder):
    .\Deploy.ps1 -Server "IFSBK\WEBAPPDB" -User sa            # real DB, no demo seed
    .\Deploy.ps1 -Server "IFSBK\WEBAPPDB" -User sa -IncludeSeed   # TEST DB, adds demo users E0001..E0005
    .\Deploy.ps1 -Server "localhost"                          # Windows authentication

  The password is asked interactively and is never written to disk.
#>
param(
    [Parameter(Mandatory = $true)][string]$Server,
    [string]$User,
    [switch]$IncludeSeed
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
$runFile = Join-Path $PSScriptRoot '00_Deploy_Run.tmp.sql'

try {
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        throw 'sqlcmd was not found in PATH.'
    }

    # 1. Build the effective master (optionally enable the test seed lines)
    $lines = Get-Content '00_Deploy_All.sql' -Encoding UTF8
    if ($IncludeSeed) {
        Write-Host 'Seed ENABLED: demo users E0001..E0005 (password Test@123) will be created.' -ForegroundColor Yellow
        $lines = $lines | ForEach-Object { $_ -replace '^-- \[SEED\] ', '' }
    }
    $lines | Set-Content $runFile -Encoding UTF8

    # 2. Every :r file must exist
    $files = Select-String -Path $runFile -Pattern '^:r\s+"(.+)"\s*$' |
        ForEach-Object { $_.Matches[0].Groups[1].Value }
    $missing = $files | Where-Object { -not (Test-Path -LiteralPath $_) }
    if ($missing) {
        $missing | ForEach-Object { Write-Host "MISSING: $_" -ForegroundColor Red }
        throw 'Some SQL files are missing. Nothing was executed.'
    }

    # 3. Files that do not end with a newline get glued to the next file
    #    (e.g. "END" + "USE" -> "ENDUSE"). Append a newline + GO.
    foreach ($f in ($files | Select-Object -Unique)) {
        $b = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $f))
        if ($b.Length -gt 0 -and $b[-1] -ne 10) {
            [IO.File]::AppendAllText((Resolve-Path -LiteralPath $f), "`r`nGO`r`n")
            Write-Host "Fixed missing trailing newline: $f"
        }
    }

    # 4. Authentication
    if ($User) {
        $sec  = Read-Host "Password for $User" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
        $env:SQLCMDPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        $auth = @('-U', $User)
    } else {
        $auth = @('-E')
    }

    # 5. Run
    Write-Host "Deploying $($files.Count) files to $Server ..." -ForegroundColor Cyan
    & sqlcmd -S $Server @auth -I -b -f 65001 -i $runFile 2>&1 | Tee-Object -FilePath 'deploy.log'
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'DEPLOYMENT FAILED. See the last lines above or deploy.log.' -ForegroundColor Red
        exit 1
    }
    Write-Host 'DEPLOYMENT COMPLETED.' -ForegroundColor Green
}
finally {
    Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
    if (Test-Path $runFile) { Remove-Item $runFile -Force }
    Pop-Location
}