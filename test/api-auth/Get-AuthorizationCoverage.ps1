[CmdletBinding()]
param(
    [string]$ControllersDirectory = "./FVN_REGISTER.API/Controllers",
    [string]$BaselinePath = "./test/api-auth/authorization-baseline.txt",
    [string]$OutputDirectory = "./artifacts/api-auth"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
# Static review aid (no build/network/DB). The API authenticates with [Authorize], but function/scope authorization is done
# in code. A controller with NO such reference is either self-scoped (acts only on the current user) or a real gap.
# Every such controller must be listed in the baseline with a reason; an unlisted one fails the check.

$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
$pattern = 'SecurityFunctionCodes\.|IAuthorizationService|_authorization|HasAsync|HasPersonal|HasManagement|CanOperateAsync|\bRoles\s*=|\bPolicy\s*=|IsSuperAdmin|UserPermissionCodes\.|\.Permission\b'
$baseline = @{}
if (Test-Path $BaselinePath) {
    foreach ($line in Get-Content -Encoding UTF8 $BaselinePath) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) { continue }
        $parts = $line -split '\|', 3
        $baseline[$parts[0].Trim()] = @{ Status = $parts[1].Trim(); Reason = $(if ($parts.Count -gt 2) { $parts[2].Trim() } else { "" }) }
    }
}
$rows = foreach ($f in Get-ChildItem $ControllersDirectory -Filter *Controller.cs -File) {
    $name = $f.BaseName
    if ($name -eq "BaseApiController") { continue }
    $text = Get-Content -Raw -Encoding UTF8 $f.FullName
    $endpoints = [regex]::Matches($text, '\bHttp(?:Get|Post|Put|Delete|Patch)\b').Count
    $refs = [regex]::Matches($text, $pattern).Count
    [pscustomobject]@{ Controller = $name; Endpoints = $endpoints; AuthorizationReferences = $refs
        InCodeCheck = ($refs -gt 0); Baseline = $(if ($baseline.ContainsKey($name)) { $baseline[$name].Status } else { "" }) }
}
$rows = @($rows | Sort-Object AuthorizationReferences, Controller)
$rows | Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $OutputDirectory "authorization-coverage.csv")

$noCheck = @($rows | Where-Object { -not $_.InCodeCheck })
$unreviewed = @($noCheck | Where-Object { $_.Baseline -eq "" })
$gaps = @($noCheck | Where-Object { $_.Baseline -eq "GAP" })
$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# API authorization coverage (static)"); $md.Add("")
$md.Add("Controllers without any in-code authorization reference: **$($noCheck.Count)** (unreviewed: **$($unreviewed.Count)**, known GAP: **$($gaps.Count)**)"); $md.Add("")
$md.Add("| Controller | Endpoints | Baseline | Reason |"); $md.Add("|---|---:|---|---|")
foreach ($r in $noCheck) { $reason = if ($baseline.ContainsKey($r.Controller)) { $baseline[$r.Controller].Reason } else { "NOT REVIEWED" }; $md.Add("| $($r.Controller) | $($r.Endpoints) | $($r.Baseline) | $reason |") }
$md | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "authorization-coverage.md")
if ($env:GITHUB_STEP_SUMMARY) { $md | Add-Content -Encoding utf8 $env:GITHUB_STEP_SUMMARY }
foreach ($g in $gaps) { Write-Host "::warning::Known authorization GAP: $($g.Controller) ($($g.Endpoints) endpoints) - $($baseline[$g.Controller].Reason)" }
foreach ($u in $unreviewed) { Write-Host "::error::$($u.Controller) has no in-code authorization and is not in authorization-baseline.txt" }
Write-Host "No-check controllers: $($noCheck.Count); unreviewed: $($unreviewed.Count); known gaps: $($gaps.Count)"
if ($unreviewed.Count -gt 0) { exit 1 }
