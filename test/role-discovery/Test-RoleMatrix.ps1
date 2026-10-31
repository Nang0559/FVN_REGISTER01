[CmdletBinding()]
param(
    [string]$DiscoveryDirectory = "./artifacts/role-discovery",
    [string]$OutputDirectory = "./artifacts/role-matrix",
    [string[]]$RequiredRequestTypes = @(),
    [switch]$FailOnWarn
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$jsonPath = Join-Path $DiscoveryDirectory "role-discovery.json"
if (-not (Test-Path $jsonPath)) { throw "Discovery file not found: $jsonPath. Run Discover-UserRoles.ps1 first." }
$d = Get-Content -Raw -Encoding UTF8 $jsonPath | ConvertFrom-Json
$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
$RequiredRequestTypes = @($RequiredRequestTypes | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

function As-Array($x) { if ($null -eq $x) { ,@() } else { ,@($x) } }
$users = @(As-Array $d.Users)[0]
$grants = @(As-Array $d.FunctionGrants)[0]
$rules = @(As-Array $d.HrmRoleRules)[0]
$approvers = @(As-Array $d.Approvers)[0]

$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Id, [string]$Title, [ValidateSet("PASS","WARN","FAIL","INFO")][string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ Id=$Id; Title=$Title; Status=$Status; Detail=$Detail })
}
function Prop($o, [string]$n) { if ($null -ne $o -and $o.PSObject.Properties.Name -contains $n) { $o.$n } else { $null } }

if ($users.Count -eq 0) { Add-Check "ROLE-001" "Active users exist" "FAIL" "No active users in dbo.F03Users." }
else { Add-Check "ROLE-001" "Active users exist" "PASS" "$($users.Count) active users." }

$noGroup = @($users | Where-Object { $null -eq (Prop $_ "PermissionCode") -or [string]::IsNullOrWhiteSpace([string](Prop $_ "PermissionName")) })
if ($noGroup.Count -gt 0) {
    $sample = ($noGroup | Select-Object -First 10 | ForEach-Object { $_.EmployeeCode }) -join ", "
    Add-Check "ROLE-002" "Active users map to a permission group" "FAIL" "$($noGroup.Count) active user(s) have no/unknown PermissionCode. Sample: $sample"
} else { Add-Check "ROLE-002" "Active users map to a permission group" "PASS" "All active users have a known permission group." }

$grantedUserIds = @{}
foreach ($g in $grants) { $grantedUserIds["$($g.UserId)"] = $true }
$noGrant = @($users | Where-Object { -not $grantedUserIds.ContainsKey("$($_.UserId)") })
if ($noGrant.Count -eq 0) { Add-Check "ROLE-003" "Active users have direct function grants" "PASS" "Every active user has at least one grant." }
else {
    $groups = ($noGrant | Group-Object PermissionCode | ForEach-Object { "PermissionCode $($_.Name): $($_.Count)" }) -join "; "
    Add-Check "ROLE-003" "Active users have direct function grants" "INFO" "$($noGrant.Count) active user(s) have no direct grant (may rely on code-level permission group). By group: $groups"
}

$knownCodes = @{}
foreach ($u in $users) { if ($null -ne (Prop $u "PermissionCode")) { $knownCodes["$($u.PermissionCode)"] = $true } }
$ruleUnknown = @($rules | Where-Object { -not $knownCodes.ContainsKey("$($_.PermissionCode)") })
if ($ruleUnknown.Count -gt 0) {
    Add-Check "ROLE-004" "HRM role rules target a PermissionCode in use" "INFO" "$($ruleUnknown.Count) rule(s) target a PermissionCode that no active user has yet: $((($ruleUnknown | Select-Object -First 10 | ForEach-Object { "Rule $($_.RuleId)->$($_.PermissionCode)" }) -join ', '))"
} else { Add-Check "ROLE-004" "HRM role rules target a PermissionCode in use" "PASS" "$($rules.Count) active rule(s) checked." }

$dupRules = @($rules | Group-Object DeptCode, PositionCode, Priority | Where-Object { $_.Count -gt 1 })
if ($dupRules.Count -gt 0) { Add-Check "ROLE-005" "HRM rules are unambiguous (Dept+Position+Priority)" "WARN" "$($dupRules.Count) ambiguous group(s): $((($dupRules | Select-Object -First 5 | ForEach-Object { $_.Name }) -join ' | '))" }
else { Add-Check "ROLE-005" "HRM rules are unambiguous (Dept+Position+Priority)" "PASS" "No ambiguous rules." }

$activeUserIds = @{}
foreach ($u in $users) { $activeUserIds["$($u.UserId)"] = $true }
$orphans = @($approvers | Where-Object { $null -eq (Prop $_ "UserId") -or -not $activeUserIds.ContainsKey("$($_.UserId)") })
if ($orphans.Count -gt 0) {
    Add-Check "APR-001" "Active approver rows resolve to an active user" "FAIL" "$($orphans.Count) approver row(s) without an active user: $((($orphans | Select-Object -First 10 | ForEach-Object { "$($_.RequestType)/dept $($_.ApproveForDeptCode)/L$($_.Level)/$($_.ApproverCode)" }) -join '; '))"
} else { Add-Check "APR-001" "Active approver rows resolve to an active user" "PASS" "$($approvers.Count) approver row(s) checked." }

$gaps = [System.Collections.Generic.List[string]]::new()
foreach ($grp in ($approvers | Group-Object RequestType, ApproveForDeptCode)) {
    $levels = @($grp.Group | ForEach-Object { [int]$_.Level } | Sort-Object -Unique)
    if ($levels.Count -eq 0) { continue }
    $expected = 1..($levels | Measure-Object -Maximum).Maximum
    $missing = @($expected | Where-Object { $_ -notin $levels })
    if ($missing.Count -gt 0) { $gaps.Add("$($grp.Name) missing level(s) $($missing -join ',')") }
}
if ($gaps.Count -gt 0) { Add-Check "APR-002" "Approval levels are contiguous from level 1" "FAIL" (($gaps | Select-Object -First 10) -join "; ") }
else { Add-Check "APR-002" "Approval levels are contiguous from level 1" "PASS" "All request type/department chains are contiguous." }

if ($RequiredRequestTypes.Count -gt 0) {
    $have = @($approvers | ForEach-Object { [string]$_.RequestType } | Select-Object -Unique)
    $missingTypes = @($RequiredRequestTypes | Where-Object { $_ -notin $have })
    if ($missingTypes.Count -gt 0) { Add-Check "APR-003" "Required request types have approvers" "FAIL" "No active approver rows for: $($missingTypes -join ', '). Present: $($have -join ', ')" }
    else { Add-Check "APR-003" "Required request types have approvers" "PASS" "Chains found for $($RequiredRequestTypes -join ', ')." }
} else {
    Add-Check "APR-003" "Request types discovered" "INFO" "Types in F03Approvers: $((@($approvers | ForEach-Object { [string]$_.RequestType } | Select-Object -Unique)) -join ', ')"
}

$approveGrantUsers = @{}
foreach ($g in $grants) { if ("$($g.ActionCode)" -match "Approv") { $approveGrantUsers["$($g.UserId)"] = $true } }
$noApproveGrant = @($approvers | Where-Object { $null -ne (Prop $_ "UserId") -and $activeUserIds.ContainsKey("$($_.UserId)") -and -not $approveGrantUsers.ContainsKey("$($_.UserId)") })
if ($noApproveGrant.Count -gt 0) {
    Add-Check "APR-004" "Routed approvers hold an Approve* grant" "WARN" "$($noApproveGrant.Count) routed approver(s) have no direct Approve* grant (server may authorize by permission group; verify): $((($noApproveGrant | Select-Object -First 8 | ForEach-Object { $_.EmployeeCode }) -join ', '))"
} else { Add-Check "APR-004" "Routed approvers hold an Approve* grant" "PASS" "All routed approvers have an Approve* grant." }

foreach ($module in @("Leave", "Overtime")) {
    $own = @($grants | Where-Object { $_.ModuleCode -eq $module -and $_.ScopeCode -eq "Own" -and "$($_.ActionCode)" -in @("Create","Register") })
    if ($own.Count -eq 0) { Add-Check "SELF-$module" "$module self-service (Create/Own) user exists" "FAIL" "No active user holds $module/Create(or Register)/Own." }
    else { Add-Check "SELF-$module" "$module self-service (Create/Own) user exists" "PASS" "$(@($own | Select-Object -ExpandProperty EmployeeCode -Unique).Count) user(s)." }
}

$proposal = $null
$leaveOwn = @($grants | Where-Object { $_.ModuleCode -eq "Leave" -and $_.ScopeCode -eq "Own" -and "$($_.ActionCode)" -in @("Create","Register") })
foreach ($grp in ($approvers | Group-Object RequestType, ApproveForDeptCode)) {
    $l1 = @($grp.Group | Where-Object { [int]$_.Level -eq 1 } | Select-Object -First 1)
    $l2 = @($grp.Group | Where-Object { [int]$_.Level -eq 2 } | Select-Object -First 1)
    if ($l1.Count -eq 0 -or $l2.Count -eq 0) { continue }
    $dept = $grp.Group[0].ApproveForDeptCode
    $worker = $leaveOwn | Where-Object {
        $emp = $_.EmployeeCode
        $_.EmployeeCode -notin @($l1[0].EmployeeCode, $l2[0].EmployeeCode) -and
        @($users | Where-Object { $_.EmployeeCode -eq $emp -and $_.DeptCode -eq $dept }).Count -gt 0
    } | Select-Object -First 1
    if ($null -ne $worker) {
        $proposal = [ordered]@{
            RequestType = [string]$l1[0].RequestType; DepartmentCode = $dept
            RequesterEmployeeCode = $worker.EmployeeCode
            Approver1EmployeeCode = $l1[0].EmployeeCode; Approver2EmployeeCode = $l2[0].EmployeeCode
            Note = "Proposal only. Validate against the application, then map to E2E_REQUESTER_* / E2E_APPROVER1_* / E2E_APPROVER2_* for scripts/Test-ApprovalEmailE2E.ps1 on a DISPOSABLE database."
        }
        break
    }
}
if ($null -ne $proposal) {
    $proposal | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "e2e-fixture-proposal.json")
    Add-Check "FIXTURE-001" "Two-level approval E2E fixture can be derived" "PASS" "Requester $($proposal.RequesterEmployeeCode), L1 $($proposal.Approver1EmployeeCode), L2 $($proposal.Approver2EmployeeCode), dept $($proposal.DepartmentCode)."
} else {
    Add-Check "FIXTURE-001" "Two-level approval E2E fixture can be derived" "WARN" "No department has a Leave self-service user in the same department plus approvers at levels 1 and 2."
}

$fail = @($checks | Where-Object Status -eq "FAIL").Count
$warn = @($checks | Where-Object Status -eq "WARN").Count
$overall = if ($fail -gt 0) { "FAIL" } elseif ($warn -gt 0) { "PASS_WITH_WARNINGS" } else { "PASS" }

[pscustomobject]@{ Overall=$overall; Database=$d.Database; CollectedAtUtc=$d.CollectedAtUtc; Checks=$checks } |
    ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "role-matrix.json")

$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# Role matrix validation - $overall"); $md.Add("")
$md.Add("Database: **$($d.Database)** | Collected: $($d.CollectedAtUtc)"); $md.Add("")
$md.Add("| Status | Check | Detail |"); $md.Add("|---|---|---|")
foreach ($c in $checks) { $md.Add("| $($c.Status) | $($c.Id) $($c.Title) | $(($c.Detail -replace '\|','/') -replace '\r?\n',' ') |") }
$md | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "role-matrix-report.md")
if ($env:GITHUB_STEP_SUMMARY) { $md | Add-Content -Encoding utf8 $env:GITHUB_STEP_SUMMARY }

$checks | Format-Table Status, Id, Title -AutoSize | Out-String | Write-Host
Write-Host "Role matrix: $overall (FAIL=$fail, WARN=$warn)"
if ($fail -gt 0 -or ($FailOnWarn -and $warn -gt 0)) { exit 1 }
