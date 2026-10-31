[CmdletBinding()]
param([string]$OutputDirectory = "./artifacts/role-discovery")
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$cs = $env:FVN_SQL_CONNECTION_STRING
if ([string]::IsNullOrWhiteSpace($cs)) { throw "FVN_SQL_CONNECTION_STRING is required." }
Add-Type -AssemblyName System.Data
$conn = [System.Data.SqlClient.SqlConnection]::new($cs)

function Read-Query([string]$Sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = 90
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($cmd)
    $table = [System.Data.DataTable]::new()
    [void]$adapter.Fill($table)
    $adapter.Dispose()
    $cmd.Dispose()
    return ,$table
}
function Rows($table) {
    $result = @()
    foreach ($row in $table.Rows) {
        $item = [ordered]@{}
        foreach ($column in $table.Columns) {
            $v = $row[$column.ColumnName]
            if ($v -is [System.DBNull]) { $v = $null }
            $item[$column.ColumnName] = $v
        }
        $result += [pscustomobject]$item
    }
    return ,$result
}

$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
$conn.Open()
try {
    $meta = (Rows (Read-Query @"
SET NOCOUNT ON;
SELECT DB_NAME() DatabaseName, CAST(SERVERPROPERTY('ServerName') AS nvarchar(256)) ServerName,
       SYSUTCDATETIME() CollectedAtUtc,
       OBJECT_ID(N'dbo.F03Users',N'U') UsersTableId,
       OBJECT_ID(N'dbo.F03Permissions',N'U') PermissionsTableId;
"@))[0]
    if ($null -eq $meta.UsersTableId -or $null -eq $meta.PermissionsTableId) {
        throw "Required dbo.F03Users or dbo.F03Permissions table is missing."
    }

    $schema = Read-Query @"
SET NOCOUNT ON;
SELECT s.name SchemaName,t.name TableName,c.column_id ColumnOrder,c.name ColumnName,
       ty.name DataType,c.max_length MaxLength,c.is_nullable IsNullable
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.columns c ON c.object_id=t.object_id
JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE s.name=N'dbo' AND (
 t.name LIKE N'F03%User%' OR t.name LIKE N'F03%Role%' OR
 t.name LIKE N'F03%Permission%' OR t.name LIKE N'F03%Function%' OR
 t.name LIKE N'F03%Approver%' OR t.name IN
 (N'F03Employees',N'F03Positions',N'F03Departments'))
ORDER BY t.name,c.column_id;
"@

    $users = Read-Query @"
SET NOCOUNT ON;
SELECT u.Id UserId,u.EmployeeCode,u.PermissionCode,p.PermissionName,
       u.DeptCode,d.DeptName,COALESCE(e.PositionCode,u.Cvcode) PositionCode,
       pos.PositionName,pos.IsApprove,pos.IsAllowApprove,u.LevelApprove,
       u.IsActive UserIsActive,e.IsActive EmployeeIsActive
FROM dbo.F03Users u
LEFT JOIN dbo.F03Permissions p ON p.PermissionCode=u.PermissionCode
LEFT JOIN dbo.F03Employees e ON e.EmployeeCode=u.EmployeeCode
LEFT JOIN dbo.F03Departments d ON d.DeptCode=u.DeptCode
LEFT JOIN dbo.F03Positions pos ON pos.PositionCode=COALESCE(e.PositionCode,u.Cvcode)
WHERE ISNULL(u.IsActive,0)=1
ORDER BY u.PermissionCode,u.DeptCode,u.LevelApprove,u.EmployeeCode;
"@

    $grants = [System.Data.DataTable]::new()
    $hasGrants = (Read-Query "SELECT OBJECT_ID(N'dbo.F03UserFunctions',N'U') ObjectId;").Rows[0].ObjectId -ne [DBNull]::Value
    if ($hasGrants) {
        $grants = Read-Query @"
SET NOCOUNT ON;
SELECT u.Id UserId,u.EmployeeCode,u.PermissionCode,
       f.FunctionCode,f.FunctionName,f.ModuleCode,f.ActionCode,f.ScopeCode,
       uf.IsActive GrantIsActive
FROM dbo.F03Users u
JOIN dbo.F03UserFunctions uf ON uf.IdUser=u.Id
JOIN dbo.F03Functions f ON f.Id=uf.IdFunction
WHERE ISNULL(u.IsActive,0)=1 AND ISNULL(uf.IsActive,1)=1
ORDER BY u.EmployeeCode,f.ModuleCode,f.FunctionCode;
"@
    }

    $rules = [System.Data.DataTable]::new()
    $hasRules = (Read-Query "SELECT OBJECT_ID(N'dbo.F03HrmUserRoleRules',N'U') ObjectId;").Rows[0].ObjectId -ne [DBNull]::Value
    if ($hasRules) {
        $rules = Read-Query @"
SET NOCOUNT ON;
SELECT r.Id RuleId,r.DeptCode,d.DeptName,r.PositionCode,r.PermissionCode,
       p.PermissionName,r.Priority,r.Note,r.IsActive
FROM dbo.F03HrmUserRoleRules r
LEFT JOIN dbo.F03Departments d ON d.DeptCode=r.DeptCode
LEFT JOIN dbo.F03Permissions p ON p.PermissionCode=r.PermissionCode
WHERE ISNULL(r.IsActive,0)=1
ORDER BY r.Priority,r.DeptCode,r.PositionCode;
"@
    }

    $approvers = [System.Data.DataTable]::new()
    $hasApprovers = (Read-Query "SELECT OBJECT_ID(N'dbo.F03Approvers',N'U') ObjectId;").Rows[0].ObjectId -ne [DBNull]::Value
    if ($hasApprovers) {
        $approvers = Read-Query @"
SET NOCOUNT ON;
SELECT a.RequestType,a.UserId,u.EmployeeCode,a.ApproverCode,a.PositionCode,
       a.ApproverDeptCode,a.ApproveForDeptCode,a.Level,a.RoleName,a.IsActive
FROM dbo.F03Approvers a
LEFT JOIN dbo.F03Users u ON u.Id=a.UserId
WHERE ISNULL(a.IsActive,0)=1
ORDER BY a.RequestType,a.ApproveForDeptCode,a.Level,a.ApproverCode;
"@
    }
}
finally { $conn.Close(); $conn.Dispose() }

$userRows = @(Rows $users)
$grantRows = @(Rows $grants)
$ruleRows = @(Rows $rules)
$approverRows = @(Rows $approvers)
$schemaRows = @(Rows $schema)

# One suggested account per real PermissionCode; rank by number of active grants.
$representatives = @()
foreach ($group in ($userRows | Group-Object PermissionCode)) {
    $ranked = foreach ($u in $group.Group) {
        $count = @($grantRows | Where-Object { $_.UserId -eq $u.UserId }).Count
        [pscustomobject]@{ User=$u; GrantCount=$count }
    }
    $chosen = $ranked | Sort-Object @{Expression="GrantCount";Descending=$true},@{Expression={$_.User.LevelApprove};Descending=$false} | Select-Object -First 1
    $u = $chosen.User
    $representatives += [pscustomobject]@{
        PermissionCode=$u.PermissionCode; PermissionName=$u.PermissionName
        UserId=$u.UserId; EmployeeCode=$u.EmployeeCode
        DeptCode=$u.DeptCode; DeptName=$u.DeptName
        PositionCode=$u.PositionCode; PositionName=$u.PositionName
        LevelApprove=$u.LevelApprove; IsApprovePosition=$u.IsApprove
        IsAllowApprove=$u.IsAllowApprove; ActiveFunctionGrantCount=$chosen.GrantCount
        SelectionNote="Candidate only; validate HRM rules, module grants and approver routing."
    }
}

# Propose business-role candidates from evidence, not from guessed role names.
$businessCandidates = @()
$selfService = $grantRows | Where-Object {
    $_.ScopeCode -eq "Own" -and
    $_.ActionCode -in @("Create", "Register") -and
    $_.ModuleCode -in @("Leave", "Overtime")
}
foreach ($g in ($selfService | Group-Object ModuleCode,ActionCode,ScopeCode)) {
    $candidate = $g.Group | Group-Object EmployeeCode | ForEach-Object {
        $first = $_.Group | Select-Object -First 1
        [pscustomobject]@{ UserId=$first.UserId; EmployeeCode=$first.EmployeeCode; GrantCount=$_.Count; Grant=$first }
    } | Sort-Object @{Expression="GrantCount";Descending=$true} | Select-Object -First 1
    $businessCandidates += [pscustomobject]@{
        SuggestedBusinessRole = "Self-service $($candidate.Grant.ModuleCode) user"
        UserId = $candidate.UserId
        EmployeeCode = $candidate.EmployeeCode
        ModuleCode = $candidate.Grant.ModuleCode
        ActionCode = $candidate.Grant.ActionCode
        ScopeCode = $candidate.Grant.ScopeCode
        Evidence = "Active function grant"
        Confidence = "Needs UI/server authorization verification"
    }
}
foreach ($group in ($approverRows | Group-Object RequestType,ApproveForDeptCode,Level)) {
    $a = $group.Group | Select-Object -First 1
    $businessCandidates += [pscustomobject]@{
        SuggestedBusinessRole = "Approver - $($a.RequestType) - Dept $($a.ApproveForDeptCode) - Level $($a.Level)"
        UserId = $a.UserId
        EmployeeCode = $a.EmployeeCode
        ModuleCode = $a.RequestType
        ActionCode = "Approve"
        ScopeCode = "Department/approval routing"
        Evidence = "Active F03Approvers routing row"
        Confidence = "Needs matching function grant and workflow verification"
    }
}

$suggestions = @(
 [pscustomobject]@{Priority="P0";Purpose="Map active users to permission groups";Query="SELECT u.Id,u.EmployeeCode,u.PermissionCode,p.PermissionName,u.DeptCode,u.Cvcode,u.LevelApprove FROM dbo.F03Users u LEFT JOIN dbo.F03Permissions p ON p.PermissionCode=u.PermissionCode WHERE u.IsActive=1 ORDER BY u.PermissionCode,u.EmployeeCode;"},
 [pscustomobject]@{Priority="P0";Purpose="Validate HRM department/position-to-role rules";Query="SELECT r.*,p.PermissionName FROM dbo.F03HrmUserRoleRules r LEFT JOIN dbo.F03Permissions p ON p.PermissionCode=r.PermissionCode WHERE r.IsActive=1 ORDER BY r.Priority;"},
 [pscustomobject]@{Priority="P0";Purpose="Validate direct grants by module/action/scope";Query="SELECT u.EmployeeCode,f.FunctionCode,f.FunctionName,f.ModuleCode,f.ActionCode,f.ScopeCode FROM dbo.F03Users u JOIN dbo.F03UserFunctions uf ON uf.IdUser=u.Id JOIN dbo.F03Functions f ON f.Id=uf.IdFunction WHERE u.IsActive=1 AND uf.IsActive=1 ORDER BY u.EmployeeCode,f.ModuleCode,f.ActionCode;"},
 [pscustomobject]@{Priority="P0";Purpose="Validate approver chain by request type, department and level";Query="SELECT a.RequestType,a.ApproverCode,a.ApproveForDeptCode,a.Level,a.RoleName,u.EmployeeCode FROM dbo.F03Approvers a LEFT JOIN dbo.F03Users u ON u.Id=a.UserId WHERE a.IsActive=1 ORDER BY a.RequestType,a.ApproveForDeptCode,a.Level;"},
 [pscustomobject]@{Priority="P1";Purpose="Identify worker self-service candidates";Query="Filter the grant inventory to Leave/Create and Overtime/Create with Own scope; cross-check each candidate against HRM role rules."},
 [pscustomobject]@{Priority="P1";Purpose="Identify approver candidates";Query="Match active F03Approvers rows by RequestType, ApproveForDeptCode and Level; verify the matching module approval action grant."},
 [pscustomobject]@{Priority="P1";Purpose="Test own/department/all data boundaries";Query="For each module/action/scope, compare a user's own records with another employee in the same department and a different department."},
 [pscustomobject]@{Priority="P2";Purpose="Discover workflow statuses before scenario seeding";Query="Inspect request tables and status columns from the schema inventory; do not assume numeric status meanings."}
)

$report = [ordered]@{
 CollectedAtUtc=$meta.CollectedAtUtc; Server=$meta.ServerName; Database=$meta.DatabaseName
 Scope="Read-only discovery. No HRM sync, inserts, updates, deletes or permission changes."
 Counts=[ordered]@{ActiveUsers=$userRows.Count;PermissionGroups=@($userRows.PermissionCode | Select-Object -Unique).Count;ActiveFunctionGrants=$grantRows.Count;ActiveHrmRoleRules=$ruleRows.Count;ActiveApproverRows=$approverRows.Count}
 Users=$userRows;FunctionGrants=$grantRows;HrmRoleRules=$ruleRows;Approvers=$approverRows
 RepresentativeCandidates=$representatives;BusinessRoleCandidates=$businessCandidates;SuggestedQueries=$suggestions;SchemaInventory=$schemaRows
}
$report | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "role-discovery.json")
$userRows | Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $OutputDirectory "active-users.csv")
$grantRows | Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $OutputDirectory "function-grants.csv")
$representatives | Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $OutputDirectory "representative-candidates.csv")
$businessCandidates | Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $OutputDirectory "business-role-candidates.csv")
$schemaRows | Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $OutputDirectory "security-schema.csv")

$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# FVN_REGISTER - HRM User/Role Discovery")
$md.Add("")
$md.Add("- Server: **$($meta.ServerName)**")
$md.Add("- Database: **$($meta.DatabaseName)**")
$md.Add("- Active users: **$($userRows.Count)**")
$md.Add("- Permission groups: **$(@($userRows.PermissionCode | Select-Object -Unique).Count)**")
$md.Add("- Active function grants: **$($grantRows.Count)**")
$md.Add("")
$md.Add("> Candidates are proposals. PermissionCode is not automatically equivalent to a business role. Validate HRM rules, grants, approver routing and application authorization.")
$md.Add("")
$md.Add("## One candidate per existing permission group")
$md.Add("")
$md.Add("| PermissionCode | Permission | UserId | EmployeeCode | Dept | Position | Level | Grants |")
$md.Add("|---:|---|---:|---|---|---|---:|---:|")
foreach ($x in $representatives) { $md.Add("| $($x.PermissionCode) | $($x.PermissionName) | $($x.UserId) | $($x.EmployeeCode) | $($x.DeptCode) | $($x.PositionCode) | $($x.LevelApprove) | $($x.ActiveFunctionGrantCount) |") }
$md.Add("")
$md.Add("## Suggested business-role candidates")
$md.Add("")
$md.Add("| Suggested role | UserId | EmployeeCode | Module | Action | Scope | Evidence |")
$md.Add("|---|---:|---|---|---|---|---|")
foreach ($x in $businessCandidates) { $md.Add("| $($x.SuggestedBusinessRole) | $($x.UserId) | $($x.EmployeeCode) | $($x.ModuleCode) | $($x.ActionCode) | $($x.ScopeCode) | $($x.Evidence) |") }
$md.Add("")
$md.Add("## Suggested follow-up queries")
foreach ($x in $suggestions) { $md.Add(""); $md.Add("### [$($x.Priority)] $($x.Purpose)"); $md.Add(""); $md.Add($x.Query) }
$md.Add("")
$md.Add("## Validation rules")
$md.Add("- Worker: verify Leave/Create and Overtime/Create grants with Own scope.")
$md.Add("- Approver: verify active F03Approvers routing for request type, department and level, plus matching approval grant.")
$md.Add("- Admin/HR: verify each module/action/scope separately; do not infer broad access from the permission name.")
$md.Add("- Employee, department and position data are read-only HRM reference data.")
$md | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "role-discovery-report.md")

Write-Host "Discovery complete: $($userRows.Count) active users, $($representatives.Count) candidate permission groups."
