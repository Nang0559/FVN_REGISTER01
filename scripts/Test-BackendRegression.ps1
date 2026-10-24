# Run backend regression checks from the repository root.
# Usage:
#   .\scripts\Test-BackendRegression.ps1
#   .\scripts\Test-BackendRegression.ps1 -SkipRestore
# This script does not deploy SQL or mutate application data.

[CmdletBinding()]
param(
    [switch]$SkipRestore,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot '..\FVN_REGISTER.sln'
$testProject = Join-Path $PSScriptRoot '..\Tests\FVN_REGISTER.Infrastructure.Tests\FVN_REGISTER.Infrastructure.Tests.csproj'
$backendProjects = @(
    (Join-Path $PSScriptRoot '..\FVN_REGISTER.Core\FVN_REGISTER.Core.csproj'),
    (Join-Path $PSScriptRoot '..\FVN_REGISTER.Contract\FVN_REGISTER.Contract.csproj'),
    (Join-Path $PSScriptRoot '..\FVN_REGISTER.Application\FVN_REGISTER.Application.csproj'),
    (Join-Path $PSScriptRoot '..\FVN_REGISTER.Infrastructure\FVN_REGISTER.Infrastructure.csproj'),
    (Join-Path $PSScriptRoot '..\FVN_REGISTER.API\FVN_REGISTER.API.csproj')
)

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    Write-Host ''
    Write-Host ('> dotnet ' + ($Arguments -join ' ')) -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed (exit $LASTEXITCODE): dotnet $($Arguments -join ' ')"
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK not found. Install the .NET 9 SDK and retry.'
}

$results = [System.Collections.Generic.List[object]]::new()
try {
    if (-not $SkipRestore) {
        Invoke-DotNet @('restore', $solution)
    }

    if (-not $SkipBuild) {
        foreach ($project in $backendProjects) {
            if (-not (Test-Path $project)) { throw "Expected project not found: $project" }
            $timer = [System.Diagnostics.Stopwatch]::StartNew()
            Invoke-DotNet @('build', $project, '--no-restore')
            $timer.Stop()
            $results.Add([pscustomobject]@{ Check = "BUILD $([IO.Path]::GetFileName($project))"; Result = 'PASS'; Seconds = [math]::Round($timer.Elapsed.TotalSeconds, 1) })
        }
    }

    if (-not (Test-Path $testProject)) { throw "Test project not found: $testProject" }
    Invoke-DotNet @('test', $testProject, '--no-restore', '--logger', 'console;verbosity=normal')
    $results.Add([pscustomobject]@{ Check = 'Infrastructure.Tests'; Result = 'PASS'; Seconds = $null })
}
catch {
    $results.Add([pscustomobject]@{ Check = 'Regression run'; Result = 'FAIL'; Seconds = $null })
    Write-Host $_.Exception.Message -ForegroundColor Red
    throw
}
finally {
    Write-Host ''
    Write-Host 'Regression run summary' -ForegroundColor Yellow
    $results | Format-Table -AutoSize
}

Write-Host 'Passing this script does not certify API/UI end-to-end flows, production SQL schema, HRM data parity, or security scope. Execute the scenario catalogue in Tests/FULL_APPLICATION_REGRESSION_PLAN.md and record evidence separately.' -ForegroundColor Yellow
