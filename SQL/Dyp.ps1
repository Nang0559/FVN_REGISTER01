<#
  Dyp.ps1 - one-command deployment of FVN_REGISTER

  Examples:
    .\Dyp.ps1 -Server "IFSBK\WEBAPPDB" -User sa
    .\Dyp.ps1 -Server "IFSBK\WEBAPPDB" -User sa -IncludeSeed
    .\Dyp.ps1 -Server "localhost"

  Default:
    - Executes SQL/00_Deploy_All.sql in its declared dependency order.
    - Schema/migration only; no demo/test data.

  -IncludeSeed:
    - Enables the single late [SEED] include in 00_Deploy_All.sql.
    - Runs SQL/06_Seed_All_Modules.sql only after all schema/module migrations.
#>
param(
    [Parameter(Mandatory = $true)][string]$Server,
    [string]$User,
    [switch]$IncludeSeed
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
$runFile = Join-Path $PSScriptRoot '00_Deploy_Run.tmp.sql'

function Get-IncludeFiles([string]$Path) {
    return @(Select-String -Path $Path -Pattern '^:r\s+"(.+)"\s*$' |
        ForEach-Object { $_.Matches[0].Groups[1].Value })
}

function Assert-DeploymentOrder([string[]]$Files, [bool]$SeedEnabled) {
    $index = @{}
    for ($i = 0; $i -lt $Files.Count; $i++) {
        if ($index.ContainsKey($Files[$i])) {
            throw "Duplicate SQLCMD include detected: $($Files[$i])"
        }
        $index[$Files[$i]] = $i
    }

    $required = @(
        @('01_Database.sql','02A_Preflight.sql'),
        @('02A_Preflight.sql','02B_Schemas.sql'),
        @('02B_Schemas.sql','02C_TablePrerequisites.sql'),
        @('02C_TablePrerequisites.sql','03_Tables.sql'),
        @('03_Tables.sql','04_Constraints.sql'),
        @('04_Constraints.sql','05A_DepartmentCodeInt.sql'),
        @('05A_DepartmentCodeInt.sql','05_Indexes.sql'),
        @('15_PublicInformation.sql','Excel/001_SharedExcelPlatform.sql'),
        @('Excel/001_SharedExcelPlatform.sql','Excel/002_SharedExcelPlatform_Normalize.sql'),
        @('Excel/002_SharedExcelPlatform_Normalize.sql','Excel/003_MigrateLegacyExcelSchemaJson.sql'),
        @('Excel/003_MigrateLegacyExcelSchemaJson.sql','16_EquipmentFlexibleImport.sql'),
        @('16_EquipmentFlexibleImport.sql','16_EquipmentFlexibleImport_LegacyCleanup.sql'),
        @('70_RequestModuleCanonicalization.sql','71_DepartmentCodeInt.sql'),
        @('71_DepartmentCodeInt.sql','14D_SecuritySchemaVerify.sql'),
        @('14D_SecuritySchemaVerify.sql','12_Verify.sql'),
        @('12_Verify.sql','99_Verify.sql')
    )

    foreach ($pair in $required) {
        if (-not $index.ContainsKey($pair[0]) -or -not $index.ContainsKey($pair[1])) {
            throw "Deployment manifest is missing required include(s): $($pair[0]) -> $($pair[1])"
        }
        if ($index[$pair[0]] -ge $index[$pair[1]]) {
            throw "Deployment order is invalid: $($pair[0]) must run before $($pair[1])."
        }
    }

    if ($SeedEnabled) {
        if (-not $index.ContainsKey('06_Seed_All_Modules.sql')) {
            throw 'Seed was requested but 06_Seed_All_Modules.sql is not enabled in the deployment manifest.'
        }
        if ($index['71_DepartmentCodeInt.sql'] -ge $index['06_Seed_All_Modules.sql']) {
            throw 'Seed must run after the complete schema/module migration set and DepartmentCode verification.'
        }
        if ($index['06_Seed_All_Modules.sql'] -ge $index['14D_SecuritySchemaVerify.sql']) {
            # 14D is the first final verification gate; seed must precede it.
            return
        }
        throw 'Seed must run before the final security verification gate 14D_SecuritySchemaVerify.sql.'
    }
}

try {
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        throw 'sqlcmd was not found in PATH.'
    }

    # Read the single canonical deployment manifest.
    $lines = @(Get-Content '00_Deploy_All.sql' -Encoding UTF8)

    if ($IncludeSeed) {
        Write-Host 'Seed ENABLED: complete seed pack will run after all schema/module migrations.' -ForegroundColor Yellow
        $lines = $lines | ForEach-Object { $_ -replace '^-- \[SEED\] ', '' }
    }

    $lines | Set-Content $runFile -Encoding UTF8

    # Validate every SQLCMD include before connecting to SQL Server.
    $files = Get-IncludeFiles $runFile
    if ($files.Count -eq 0) {
        throw '00_Deploy_All.sql contains no SQLCMD includes.'
    }

    $missing = @($files | Where-Object { -not (Test-Path -LiteralPath $_) })
    if ($missing.Count -gt 0) {
        $missing | ForEach-Object { Write-Host "MISSING: $_" -ForegroundColor Red }
        throw 'Some SQL files are missing. Nothing was executed.'
    }

    Assert-DeploymentOrder $files $IncludeSeed

    # Do not mutate source SQL files during deployment. A missing final newline
    # is a repository defect and should be fixed in Git, not silently on the server.
    $badNewline = @(
        $files | Where-Object {
            $path = (Resolve-Path -LiteralPath $_).Path
            $bytes = [IO.File]::ReadAllBytes($path)
            $bytes.Length -gt 0 -and $bytes[-1] -ne 10
        }
    )
    if ($badNewline.Count -gt 0) {
        $badNewline | ForEach-Object { Write-Host "NO FINAL NEWLINE: $_" -ForegroundColor Red }
        throw 'One or more included SQL files have no final newline. Fix the repository files before deployment.'
    }

    if ($User) {
        $sec  = Read-Host "Password for $User" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
        try {
            $env:SQLCMDPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        }
        finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
        $auth = @('-U', $User)
    }
    else {
        $auth = @('-E')
    }

    Write-Host "Deploying $($files.Count) SQL files to $Server ..." -ForegroundColor Cyan
    & sqlcmd -S $Server @auth -I -b -f 65001 -i $runFile 2>&1 | Tee-Object -FilePath 'deploy.log'

    if ($LASTEXITCODE -ne 0) {
        Write-Host 'DEPLOYMENT FAILED. See deploy.log for details.' -ForegroundColor Red
        exit 1
    }

    Write-Host 'DEPLOYMENT COMPLETED.' -ForegroundColor Green
}
finally {
    Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
    if (Test-Path $runFile) {
        Remove-Item $runFile -Force
    }
    Pop-Location
}
