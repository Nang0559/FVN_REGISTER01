# Run backend regression checks and every repository test project from the repository root.
# Usage:
#   .\scripts\Test-BackendRegression.ps1
#   .\scripts\Test-BackendRegression.ps1 -SkipRestore
#   .\scripts\Test-BackendRegression.ps1 -FailOnSkippedSqlTests
# TRX files and a JSON summary are written under TestResults/BackendRegression_<timestamp>/.

[CmdletBinding()]
param(
    [switch]$SkipRestore,
    [switch]$SkipBuild,
    [switch]$FailOnSkippedSqlTests
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $root 'FVN_REGISTER.sln'
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$resultsDir = Join-Path $root ("TestResults\BackendRegression_{0}" -f $timestamp)
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

$backendProjects = @(
    'FVN_REGISTER.Core\FVN_REGISTER.Core.csproj',
    'FVN_REGISTER.Contract\FVN_REGISTER.Contract.csproj',
    'FVN_REGISTER.Application\FVN_REGISTER.Application.csproj',
    'FVN_REGISTER.Infrastructure\FVN_REGISTER.Infrastructure.csproj',
    'FVN_REGISTER.API\FVN_REGISTER.API.csproj',
    'FVN_REGISTER.AI.Core\FVN_REGISTER.AI.Core.csproj',
    'FVN_REGISTER.AI.Application\FVN_REGISTER.AI.Application.csproj',
    'FVN_REGISTER.AI.Infrastructure\FVN_REGISTER.AI.Infrastructure.csproj',
    'FVN_REGISTER.AI.Api\FVN_REGISTER.AI.Api.csproj',
    'FVN_REGISTER.AI.Worker\FVN_REGISTER.AI.Worker.csproj'
)
$testProjects = @(
    'Tests\FVN_REGISTER.Infrastructure.Tests\FVN_REGISTER.Infrastructure.Tests.csproj',
    'Tests\FVN_REGISTER.AI.Tests\FVN_REGISTER.AI.Tests.csproj',
    'Tests\FVN_REGISTER.Shared.Tests\FVN_REGISTER.Shared.Tests.csproj',
    'Tests\FVN_REGISTER.EndpointAgent.Tests\FVN_REGISTER.EndpointAgent.Tests.csproj'
)
$results = [System.Collections.Generic.List[object]]::new()
$overall = 'PASS'
$sqlConnection = $env:FVN_REGISTER_SQL_CONNECTION

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    Write-Host ''
    Write-Host ('> dotnet ' + ($Arguments -join ' ')) -ForegroundColor Cyan
    & dotnet @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "Command failed (exit $exitCode): dotnet $($Arguments -join ' ')"
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK not found. Install the .NET 9 SDK and retry.'
}
if (-not (Test-Path $solution)) { throw "Solution not found: $solution" }

try {
    if (-not $SkipRestore) {
        Invoke-DotNet @('restore', $solution)
    }

    if (-not $SkipBuild) {
        foreach ($relative in $backendProjects) {
            $project = Join-Path $root $relative
            if (-not (Test-Path $project)) {
                $results.Add([pscustomobject]@{ Check = "BUILD $relative"; Result = 'BLOCKED'; Passed = 0; Failed = 0; Skipped = 0; Detail = 'Project path not found' })
                throw "Expected backend project not found: $project"
            }
            $timer = [System.Diagnostics.Stopwatch]::StartNew()
            Invoke-DotNet @('build', $project, '--no-restore', '--verbosity', 'minimal')
            $timer.Stop()
            $results.Add([pscustomobject]@{ Check = "BUILD $relative"; Result = 'PASS'; Passed = 0; Failed = 0; Skipped = 0; Detail = "$([math]::Round($timer.Elapsed.TotalSeconds, 1)) sec" })
        }
    }

    foreach ($relative in $testProjects) {
        $project = Join-Path $root $relative
        if (-not (Test-Path $project)) { throw "Expected test project not found: $project" }
        $safeName = [IO.Path]::GetFileNameWithoutExtension($project)
        $trxName = "$safeName.trx"
        Invoke-DotNet @('test', $project, '--no-restore', '--logger', "trx;LogFileName=$trxName", '--results-directory', $resultsDir, '--verbosity', 'normal')

        $trxPath = Join-Path $resultsDir $trxName
        if (-not (Test-Path $trxPath)) { throw "TRX output missing for $relative: $trxPath" }
        [xml]$trx = Get-Content -Raw $trxPath
        $unitResults = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
        $passed = @($unitResults | Where-Object { $_.GetAttribute('outcome') -eq 'Passed' }).Count
        $failed = @($unitResults | Where-Object { $_.GetAttribute('outcome') -eq 'Failed' }).Count
        $skipped = @($unitResults | Where-Object { $_.GetAttribute('outcome') -in @('NotExecuted', 'Skipped') }).Count
        $detail = "TRX=$trxName; passed=$passed; failed=$failed; skipped/not-executed=$skipped"
        $result = if ($failed -gt 0) { 'FAIL' } elseif ($skipped -gt 0) { 'PASS_WITH_SKIPS' } else { 'PASS' }
        $results.Add([pscustomobject]@{ Check = $safeName; Result = $result; Passed = $passed; Failed = $failed; Skipped = $skipped; Detail = $detail })

        if ($failed -gt 0) { throw "$safeName reported $failed failed test(s). See $trxPath" }
        if ($skipped -gt 0) {
            $overall = 'PASS_WITH_SKIPS'
            Write-Warning "$safeName has $skipped skipped/not-executed test(s); this run is not a full PASS."
            if ($FailOnSkippedSqlTests -and -not $sqlConnection) {
                throw "$safeName has skipped tests and FVN_REGISTER_SQL_CONNECTION is not set. Configure a disposable SQL test database or remove -FailOnSkippedSqlTests."
            }
        }
    }

    if (-not $sqlConnection) {
        Write-Warning 'FVN_REGISTER_SQL_CONNECTION is not set. SQL integration tests marked with SqlIntegrationFactAttribute are skipped; see the TRX counts above.'
        if ($overall -eq 'PASS') { $overall = 'PASS_WITH_SKIPS' }
    }
}
catch {
    $overall = 'FAIL'
    $results.Add([pscustomobject]@{ Check = 'Regression run'; Result = 'FAIL'; Passed = 0; Failed = 1; Skipped = 0; Detail = $_.Exception.Message })
    Write-Host $_.Exception.Message -ForegroundColor Red
    throw
}
finally {
    $summary = [pscustomobject]@{
        StartedAt = $timestamp
        FinishedAt = (Get-Date).ToString('o')
        Overall = $overall
        SqlConnectionConfigured = -not [string]::IsNullOrWhiteSpace($sqlConnection)
        ResultsDirectory = $resultsDir
        Checks = $results
    }
    $summaryPath = Join-Path $resultsDir 'summary.json'
    $summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath -Encoding UTF8
    Write-Host ''
    Write-Host "Regression summary: $overall" -ForegroundColor Yellow
    $results | Format-Table -AutoSize
    Write-Host "Evidence: $resultsDir" -ForegroundColor Cyan
}

if ($overall -eq 'PASS_WITH_SKIPS') {
    Write-Host 'Some tests were skipped/not executed. Do not treat this run as full regression PASS.' -ForegroundColor Yellow
}
Write-Host 'This script does not certify API/UI E2E flows, production SQL schema, HRM parity, or security scope. Track scenario evidence in Tests/TEST_TRACEABILITY.md.' -ForegroundColor Yellow
