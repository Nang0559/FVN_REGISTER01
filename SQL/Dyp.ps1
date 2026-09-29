<#
  Dyp.ps1 - one-command deployment of FVN_REGISTER

  Examples:
    .\Dyp.ps1 -Server "IFSBK\WEBAPPDB" -User sa
    .\Dyp.ps1 -Server "IFSBK\WEBAPPDB" -User sa -IncludeSeed
    .\Dyp.ps1 -Server "localhost"

  -IncludeSeed enables the dedicated SQL/06_Seed_All_Modules.sql pack.
  The normal deployment remains schema/migration only.
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

    # Build the effective master. Only explicit [SEED] lines are enabled.
    $lines = Get-Content '00_Deploy_All.sql' -Encoding UTF8
    if ($IncludeSeed) {
        Write-Host 'Seed ENABLED: complete demo/master seed pack will run.' -ForegroundColor Yellow
        $lines = $lines | ForEach-Object { $_ -replace '^-- \[SEED\] ', '' }
    }

    $lines | Set-Content $runFile -Encoding UTF8

    # Verify every SQLCMD include exists before executing anything.
    $files = Select-String -Path $runFile -Pattern '^:r\s+"(.+)"\s*$' |
        ForEach-Object { $_.Matches[0].Groups[1].Value }
    $missing = $files | Where-Object { -not (Test-Path -LiteralPath $_) }
    if ($missing) {
        $missing | ForEach-Object { Write-Host "MISSING: $_" -ForegroundColor Red }
        throw 'Some SQL files are missing. Nothing was executed.'
    }

    # Files without a final newline can be glued to the next :r include.
    foreach ($f in ($files | Select-Object -Unique)) {
        $b = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $f))
        if ($b.Length -gt 0 -and $b[-1] -ne 10) {
            [IO.File]::AppendAllText((Resolve-Path -LiteralPath $f), "`r`nGO`r`n")
            Write-Host "Fixed missing trailing newline: $f"
        }
    }

    if ($User) {
        $sec  = Read-Host "Password for $User" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
        $env:SQLCMDPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        $auth = @('-U', $User)
    } else {
        $auth = @('-E')
    }

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
