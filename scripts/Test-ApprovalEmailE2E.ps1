[CmdletBinding()]
param(
 [string]$ApiBaseUrl=$env:E2E_API_BASE_URL,
 [string]$SqlConnectionString=$env:E2E_SQL_CONNECTION_STRING,
 [string]$RunId=("TST_E2E_"+(Get-Date -Format "yyyyMMdd_HHmmss")),
 [string]$LeaveTypeCode=$env:E2E_LEAVE_TYPE_CODE,
 [string]$RequesterUserName=$env:E2E_REQUESTER_USERNAME,[string]$RequesterPassword=$env:E2E_REQUESTER_PASSWORD,
 [string]$RequesterEmployeeCode=$env:E2E_REQUESTER_EMPLOYEE_CODE,[string]$RequesterEmail=$env:E2E_REQUESTER_EMAIL,
 [string]$Approver1UserName=$env:E2E_APPROVER1_USERNAME,[string]$Approver1Password=$env:E2E_APPROVER1_PASSWORD,
 [string]$Approver1EmployeeCode=$env:E2E_APPROVER1_EMPLOYEE_CODE,[string]$Approver1Email=$env:E2E_APPROVER1_EMAIL,
 [string]$Approver2UserName=$env:E2E_APPROVER2_USERNAME,[string]$Approver2Password=$env:E2E_APPROVER2_PASSWORD,
 [string]$Approver2EmployeeCode=$env:E2E_APPROVER2_EMPLOYEE_CODE
)
Set-StrictMode -Version Latest
$ErrorActionPreference="Stop"
$startedAt=Get-Date
$results=[System.Collections.Generic.List[object]]::new()

function Assert-Config([string]$Name,[string]$Value) { if ([string]::IsNullOrWhiteSpace($Value)) { throw "Missing required configuration: $Name" } }
function Add-BlockedResult([string]$Name,[string]$Detail) {
 $results.Add([pscustomobject]@{Test=$Name;Result="BLOCKED";Detail=$Detail})
 Write-Warning ("[BLOCKED] {0}: {1}" -f $Name,$Detail)
}
function Test-ContainsExactRequestId($Json,[int]$RequestId) {
 if ($null -eq $Json) { return $false }
 if ($Json -is [string]) { try { $Json | ConvertFrom-Json | ForEach-Object { return (Test-ContainsExactRequestId $_ $RequestId) } } catch {}; return $false }
 if ($Json -is [System.Collections.IDictionary]) {
  foreach($key in $Json.Keys) {
   $value=$Json[$key]
   if ($key -match '^(?i)(requestId|id)
 $results.Add([pscustomobject]@{Test=$Name;Result=$(if($Passed){"PASS"}else{"FAIL"});Detail=$Detail})
 Write-Host ("[{0}] {1}: {2}" -f $(if($Passed){"PASS"}else{"FAIL"}),$Name,$Detail)
 if(-not $Passed){throw "Assertion failed: $Name - $Detail"}
}
function Invoke-Api([string]$Method,[string]$Path,[string]$Token,$Body=$null) {
 $headers=@{}; if($Token){$headers.Authorization="Bearer $Token"}
 $args=@{Method=$Method;Uri=($ApiBaseUrl.TrimEnd("/")+$Path);Headers=$headers;ContentType="application/json";ErrorAction="Stop"}
 if($null -ne $Body){$args.Body=($Body|ConvertTo-Json -Depth 20 -Compress)}
 try {$r=Invoke-WebRequest @args; $j=$null;if($r.Content){$j=$r.Content|ConvertFrom-Json};return [pscustomobject]@{StatusCode=[int]$r.StatusCode;Json=$j;Content=$r.Content}}
 catch { $status=0;$bodyText="";if($_.Exception.Response){try{$status=[int]$_.Exception.Response.StatusCode}catch{};try{$sr=[IO.StreamReader]::new($_.Exception.Response.GetResponseStream());$bodyText=$sr.ReadToEnd();$sr.Dispose()}catch{}};throw "HTTP $Method $Path failed status=$status body=$bodyText $($_.Exception.Message)" }
}
function Get-Token($Response) {
 $json = $Response.Content
 if ($json -match '(?i)"token"\s*:\s*"([^"]+)"') { return [string]$Matches[1] }
 throw "No token in login response: $($Response.Content)"
}
function Invoke-SqlScalar([string]$Query,[hashtable]$Parameters=@{}) {
 $c=[System.Data.SqlClient.SqlConnection]::new($SqlConnectionString)
 try{$c.Open();$cmd=$c.CreateCommand();$cmd.CommandText=$Query;$cmd.CommandTimeout=30
 foreach($k in $Parameters.Keys){$p=$cmd.Parameters.Add("@$k",[System.Data.SqlDbType]::NVarChar,4000);$p.Value=[string]$Parameters[$k]}
 return $cmd.ExecuteScalar()
 }finally{if($c.State -ne "Closed"){$c.Close()};$c.Dispose()}
}
try {
 $required=@{
 E2E_API_BASE_URL=$ApiBaseUrl;E2E_SQL_CONNECTION_STRING=$SqlConnectionString;E2E_LEAVE_TYPE_CODE=$LeaveTypeCode
 E2E_REQUESTER_USERNAME=$RequesterUserName;E2E_REQUESTER_PASSWORD=$RequesterPassword;E2E_REQUESTER_EMPLOYEE_CODE=$RequesterEmployeeCode;E2E_REQUESTER_EMAIL=$RequesterEmail
 E2E_APPROVER1_USERNAME=$Approver1UserName;E2E_APPROVER1_PASSWORD=$Approver1Password;E2E_APPROVER1_EMPLOYEE_CODE=$Approver1EmployeeCode;E2E_APPROVER1_EMAIL=$Approver1Email
 E2E_APPROVER2_USERNAME=$Approver2UserName;E2E_APPROVER2_PASSWORD=$Approver2Password;E2E_APPROVER2_EMPLOYEE_CODE=$Approver2EmployeeCode
 }
 foreach($k in $required.Keys){Assert-Config $k $required[$k]}
 if($env:E2E_ALLOW_RUN -ne "YES"){throw "Safety stop: set E2E_ALLOW_RUN=YES only for a disposable test environment."}
 $db=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($SqlConnectionString)
 if($db.InitialCatalog -notmatch '(?i)(test|qa|e2e|automation)'){throw "Safety stop: database '$($db.InitialCatalog)' must contain Test, QA, E2E, or Automation."}
 if($ApiBaseUrl -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?(/|$)'){throw "Safety stop: first-run runner only permits localhost API."}

 # Refuse to run when active/default dispatch profiles could send to real SMTP.
 $fakePort = 1025
 if($env:E2E_FAKE_SMTP_PORT){$fakePort=[int]$env:E2E_FAKE_SMTP_PORT}
 $unsafeProfiles = [int](Invoke-SqlScalar @"
SELECT COUNT(*)
FROM dbo.F03EmailProfiles p
WHERE p.IsActive = 1
  AND (p.IsDefault = 1 OR EXISTS (
      SELECT 1 FROM dbo.F03EmailDispatchPolicies d
      WHERE d.IsActive = 1 AND d.EmailProfileCode = p.Code
  ))
  AND NOT (
      LOWER(p.EmailServerName) IN ('localhost','127.0.0.1','::1','host.docker.internal')
      OR LOWER(p.EmailServerName) LIKE '%mailpit%'
      OR LOWER(p.EmailServerName) LIKE '%smtp4dev%'
  );
"@)
 if($unsafeProfiles -gt 0){throw "Safety stop: $unsafeProfiles active/default email profile(s) point outside a local SMTP capture host. Configure Mailpit/smtp4dev in the disposable test DB before running."}
 $wrongPorts = [int](Invoke-SqlScalar @"
SELECT COUNT(*)
FROM dbo.F03EmailProfiles p
WHERE p.IsActive = 1
  AND (p.IsDefault = 1 OR EXISTS (
      SELECT 1 FROM dbo.F03EmailDispatchPolicies d
      WHERE d.IsActive = 1 AND d.EmailProfileCode = p.Code
  ))
  AND p.EmailServerPort <> @port;
"@ @{port="$fakePort"})
 if($wrongPorts -gt 0){throw "Safety stop: active/default email profile port does not match E2E_FAKE_SMTP_PORT=$fakePort. Configure the local capture service."}

 # Authenticate all three actors. This also confirms the API is reachable.
 $loginBody={param($u,$p) @{UserName=$u;Password=$p;DeviceId=[guid]::NewGuid().ToString();DeviceType="E2E";DeviceName="E2E Regression";RememberMe=$false}}
 $requesterToken=Get-Token (Invoke-Api POST "/api/Auth/login" "" (&$loginBody $RequesterUserName $RequesterPassword))
 $approver1Token=Get-Token (Invoke-Api POST "/api/Auth/login" "" (&$loginBody $Approver1UserName $Approver1Password))
 $approver2Token=Get-Token (Invoke-Api POST "/api/Auth/login" "" (&$loginBody $Approver2UserName $Approver2Password))
 Add-Result "Login requester + approvers" $true "All three accounts authenticated"

 $date=(Get-Date).Date.AddDays(14);while($date.DayOfWeek -in @([DayOfWeek]::Saturday,[DayOfWeek]::Sunday)){$date=$date.AddDays(1)}
 $reason="$RunId approval-email E2E"
 $body=@{Id=0;StartDate=$date.ToString("yyyy-MM-dd");EndDate=$date.ToString("yyyy-MM-dd");LeaveTypeCode=$LeaveTypeCode;Reason=$reason
 Details=@(@{LeaveDate=$date.ToString("yyyy-MM-dd");LeaveTypeCode=$LeaveTypeCode;IsHalfDay=$false;HalfDayOption=$null})
 ApprovalSelections=@(@{Level=1;ApproverCode=$Approver1EmployeeCode},@{Level=2;ApproverCode=$Approver2EmployeeCode})}
 $created=Invoke-Api POST "/api/LeaveDays/create" $requesterToken $body
 Add-Result "Submit registration" ($created.StatusCode -eq 200 -and $created.Json.IsSuccess -eq $true) "POST /api/LeaveDays/create"
 $id=[int](Invoke-SqlScalar "SELECT TOP(1) Id FROM dbo.F03LeaveDays WHERE EmployeeCode=@employee AND LeaveReason=@reason ORDER BY Id DESC" @{employee=$RequesterEmployeeCode;reason=$reason})
 if($id -le 0){throw "Could not resolve created request ID."}
 Add-Result "SQL request persisted" $true "dbo.F03LeaveDays.Id=$id; marker=$RunId"

 $snapshotCount=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03ApprovalSnapshots WHERE RequestId=@id" @{id=$id})
 Add-Result "Approval snapshot persisted" ($snapshotCount -eq 1) "snapshot count=$snapshotCount"

 $pending2Before=Invoke-Api GET "/api/LeaveDays/pending" $approver2Token
 Add-Result "Level 2 hidden before level 1" (-not (Test-ContainsExactRequestId $pending2Before.Json $id)) "Level 2 must not see actionable request before level 1 decision"
 $pending1=Invoke-Api GET "/api/LeaveDays/pending" $approver1Token
 Add-Result "Level 1 inbox" (Test-ContainsExactRequestId $pending1.Json $id) "Exact request ID is present for first approver"
 $a1=Invoke-Api POST "/api/LeaveDays/approve" $approver1Token @{Ids=@($id);Level=1;Comment="$RunId level 1"}
 Add-Result "Approve level 1" ($a1.StatusCode -eq 200 -and $a1.Json.IsSuccess -eq $true) "First decision accepted"

 $pending2=Invoke-Api GET "/api/LeaveDays/pending" $approver2Token
 Add-Result "Level 2 inbox" (Test-ContainsExactRequestId $pending2.Json $id) "Exact request ID appears for second approver only after level 1"
 $a2=Invoke-Api POST "/api/LeaveDays/approve" $approver2Token @{Ids=@($id);Level=2;Comment="$RunId level 2"}
 Add-Result "Approve level 2" ($a2.StatusCode -eq 200 -and $a2.Json.IsSuccess -eq $true) "Final decision accepted"

 $details=Invoke-Api GET "/api/LeaveDays/details/$id" $requesterToken
 $finalStatus=[int](Invoke-SqlScalar "SELECT RequestStatus FROM dbo.F03LeaveDays WHERE Id=@id" @{id=$id})
 Add-Result "Requester reads final result" ($details.StatusCode -eq 200 -and $finalStatus -eq 3) "Details endpoint status=$($details.StatusCode); SQL RequestStatus=$finalStatus (Approved=3)"

 $historyExists=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM sys.tables WHERE name='F03ApprovalHistories'")
 if($historyExists -gt 0){
  $historyCount=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03ApprovalHistories WHERE RequestId=@id" @{id=$id})
  Add-Result "SQL approval history" ($historyCount -eq 2) "history rows=$historyCount"
 }else{
  Add-BlockedResult "SQL approval history" "Table dbo.F03ApprovalHistories was not found; verify the mapped table name. Continuing remaining assertions."
 }

 # Correlate numeric IDs with a JSON/text token boundary; do not let request 12 match request 112.
 $idText=[string]$id
 $emailParams=@{started=$startedAt.ToString("s");email=$Approver1Email;p1='%"requestId":'+$idText+',%';p2='%"requestId":'+$idText+'}%';p3='%"RequestId":'+$idText+',%';p4='%"RequestId":'+$idText+'}%';p5='%Request ID: '+$idText+' %'}
 $emailQuery=@"
SELECT COUNT(*) FROM dbo.F03EmailQueues
WHERE CreatedAt>=@started AND ToEmail=@email
 AND (Payload LIKE @p1 OR Payload LIKE @p2 OR Payload LIKE @p3 OR Payload LIKE @p4
      OR Body LIKE @p1 OR Body LIKE @p2 OR Body LIKE @p3 OR Body LIKE @p4
      OR Subject LIKE @p5);
"@
 $approverQueue=[int](Invoke-SqlScalar $emailQuery $emailParams)
 $emailParams.email=$RequesterEmail
 $requesterQueue=[int](Invoke-SqlScalar $emailQuery $emailParams)
 Add-Result "Submission email exactly once" ($approverQueue -eq 1) "Correlated email rows to level 1=$approverQueue (expected 1)"
 Add-Result "Final result email exactly once" ($requesterQueue -eq 1) "Correlated email rows to requester=$requesterQueue (expected 1)"

 $out=Join-Path (Get-Location) "TestResults";New-Item -ItemType Directory -Force -Path $out|Out-Null
 $overall=if(@($results|Where-Object Result -eq "BLOCKED").Count -gt 0){"PASS_WITH_BLOCKED_ASSERTIONS"}else{"PASS"}
 $report=[pscustomobject]@{RunId=$RunId;StartedAt=$startedAt.ToString("o");FinishedAt=(Get-Date).ToString("o");ApiBaseUrl=$ApiBaseUrl;Database=$db.InitialCatalog;RequestId=$id;Overall=$overall;Results=$results}
 $path=Join-Path $out "$RunId.json";$report|ConvertTo-Json -Depth 20|Set-Content $path -Encoding UTF8
 if($overall -eq "PASS"){Write-Host "E2E PASS. RequestId=$id; evidence=$path" -ForegroundColor Green}else{Write-Warning "E2E completed with blocked assertions, not a full PASS. RequestId=$id; evidence=$path"}
}catch{
 $out=Join-Path (Get-Location) "TestResults";New-Item -ItemType Directory -Force -Path $out|Out-Null
 $report=[pscustomobject]@{RunId=$RunId;StartedAt=$startedAt.ToString("o");FinishedAt=(Get-Date).ToString("o");Error=$_.Exception.Message;Overall="FAIL_OR_BLOCKED";Results=$results}
 $path=Join-Path $out "$RunId.json";$report|ConvertTo-Json -Depth 20|Set-Content $path -Encoding UTF8
 Write-Error "$($_.Exception.Message) Evidence: $path";exit 1
} -and $null -ne $value -and [string]$value -eq [string]$RequestId) { return $true }
   if (Test-ContainsExactRequestId $value $RequestId) { return $true }
  }
  return $false
 }
 if ($Json -is [System.Collections.IEnumerable]) {
  foreach($item in $Json) { if (Test-ContainsExactRequestId $item $RequestId) { return $true } }
  return $false
 }
 if ($Json -is [pscustomobject]) {
  foreach($prop in $Json.PSObject.Properties) {
   if ($prop.Name -match '^(?i)(requestId|id)
 $results.Add([pscustomobject]@{Test=$Name;Result=$(if($Passed){"PASS"}else{"FAIL"});Detail=$Detail})
 Write-Host ("[{0}] {1}: {2}" -f $(if($Passed){"PASS"}else{"FAIL"}),$Name,$Detail)
 if(-not $Passed){throw "Assertion failed: $Name - $Detail"}
}
function Invoke-Api([string]$Method,[string]$Path,[string]$Token,$Body=$null) {
 $headers=@{}; if($Token){$headers.Authorization="Bearer $Token"}
 $args=@{Method=$Method;Uri=($ApiBaseUrl.TrimEnd("/")+$Path);Headers=$headers;ContentType="application/json";ErrorAction="Stop"}
 if($null -ne $Body){$args.Body=($Body|ConvertTo-Json -Depth 20 -Compress)}
 try {$r=Invoke-WebRequest @args; $j=$null;if($r.Content){$j=$r.Content|ConvertFrom-Json};return [pscustomobject]@{StatusCode=[int]$r.StatusCode;Json=$j;Content=$r.Content}}
 catch { $status=0;$bodyText="";if($_.Exception.Response){try{$status=[int]$_.Exception.Response.StatusCode}catch{};try{$sr=[IO.StreamReader]::new($_.Exception.Response.GetResponseStream());$bodyText=$sr.ReadToEnd();$sr.Dispose()}catch{}};throw "HTTP $Method $Path failed status=$status body=$bodyText $($_.Exception.Message)" }
}
function Get-Token($Response) {
 $json = $Response.Content
 if ($json -match '(?i)"token"\s*:\s*"([^"]+)"') { return [string]$Matches[1] }
 throw "No token in login response: $($Response.Content)"
}
function Invoke-SqlScalar([string]$Query,[hashtable]$Parameters=@{}) {
 $c=[System.Data.SqlClient.SqlConnection]::new($SqlConnectionString)
 try{$c.Open();$cmd=$c.CreateCommand();$cmd.CommandText=$Query;$cmd.CommandTimeout=30
 foreach($k in $Parameters.Keys){$p=$cmd.Parameters.Add("@$k",[System.Data.SqlDbType]::NVarChar,4000);$p.Value=[string]$Parameters[$k]}
 return $cmd.ExecuteScalar()
 }finally{if($c.State -ne "Closed"){$c.Close()};$c.Dispose()}
}
try {
 $required=@{
 E2E_API_BASE_URL=$ApiBaseUrl;E2E_SQL_CONNECTION_STRING=$SqlConnectionString;E2E_LEAVE_TYPE_CODE=$LeaveTypeCode
 E2E_REQUESTER_USERNAME=$RequesterUserName;E2E_REQUESTER_PASSWORD=$RequesterPassword;E2E_REQUESTER_EMPLOYEE_CODE=$RequesterEmployeeCode;E2E_REQUESTER_EMAIL=$RequesterEmail
 E2E_APPROVER1_USERNAME=$Approver1UserName;E2E_APPROVER1_PASSWORD=$Approver1Password;E2E_APPROVER1_EMPLOYEE_CODE=$Approver1EmployeeCode;E2E_APPROVER1_EMAIL=$Approver1Email
 E2E_APPROVER2_USERNAME=$Approver2UserName;E2E_APPROVER2_PASSWORD=$Approver2Password;E2E_APPROVER2_EMPLOYEE_CODE=$Approver2EmployeeCode
 }
 foreach($k in $required.Keys){Assert-Config $k $required[$k]}
 if($env:E2E_ALLOW_RUN -ne "YES"){throw "Safety stop: set E2E_ALLOW_RUN=YES only for a disposable test environment."}
 $db=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($SqlConnectionString)
 if($db.InitialCatalog -notmatch '(?i)(test|qa|e2e|automation)'){throw "Safety stop: database '$($db.InitialCatalog)' must contain Test, QA, E2E, or Automation."}
 if($ApiBaseUrl -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?(/|$)'){throw "Safety stop: first-run runner only permits localhost API."}

 # Refuse to run when active/default dispatch profiles could send to real SMTP.
 $fakePort = 1025
 if($env:E2E_FAKE_SMTP_PORT){$fakePort=[int]$env:E2E_FAKE_SMTP_PORT}
 $unsafeProfiles = [int](Invoke-SqlScalar @"
SELECT COUNT(*)
FROM dbo.F03EmailProfiles p
WHERE p.IsActive = 1
  AND (p.IsDefault = 1 OR EXISTS (
      SELECT 1 FROM dbo.F03EmailDispatchPolicies d
      WHERE d.IsActive = 1 AND d.EmailProfileCode = p.Code
  ))
  AND NOT (
      LOWER(p.EmailServerName) IN ('localhost','127.0.0.1','::1','host.docker.internal')
      OR LOWER(p.EmailServerName) LIKE '%mailpit%'
      OR LOWER(p.EmailServerName) LIKE '%smtp4dev%'
  );
"@)
 if($unsafeProfiles -gt 0){throw "Safety stop: $unsafeProfiles active/default email profile(s) point outside a local SMTP capture host. Configure Mailpit/smtp4dev in the disposable test DB before running."}
 $wrongPorts = [int](Invoke-SqlScalar @"
SELECT COUNT(*)
FROM dbo.F03EmailProfiles p
WHERE p.IsActive = 1
  AND (p.IsDefault = 1 OR EXISTS (
      SELECT 1 FROM dbo.F03EmailDispatchPolicies d
      WHERE d.IsActive = 1 AND d.EmailProfileCode = p.Code
  ))
  AND p.EmailServerPort <> @port;
"@ @{port="$fakePort"})
 if($wrongPorts -gt 0){throw "Safety stop: active/default email profile port does not match E2E_FAKE_SMTP_PORT=$fakePort. Configure the local capture service."}

 # Authenticate all three actors. This also confirms the API is reachable.
 $loginBody={param($u,$p) @{UserName=$u;Password=$p;DeviceId=[guid]::NewGuid().ToString();DeviceType="E2E";DeviceName="E2E Regression";RememberMe=$false}}
 $requesterToken=Get-Token (Invoke-Api POST "/api/Auth/login" "" (&$loginBody $RequesterUserName $RequesterPassword))
 $approver1Token=Get-Token (Invoke-Api POST "/api/Auth/login" "" (&$loginBody $Approver1UserName $Approver1Password))
 $approver2Token=Get-Token (Invoke-Api POST "/api/Auth/login" "" (&$loginBody $Approver2UserName $Approver2Password))
 Add-Result "Login requester + approvers" $true "All three accounts authenticated"

 $date=(Get-Date).Date.AddDays(14);while($date.DayOfWeek -in @([DayOfWeek]::Saturday,[DayOfWeek]::Sunday)){$date=$date.AddDays(1)}
 $reason="$RunId approval-email E2E"
 $body=@{Id=0;StartDate=$date.ToString("yyyy-MM-dd");EndDate=$date.ToString("yyyy-MM-dd");LeaveTypeCode=$LeaveTypeCode;Reason=$reason
 Details=@(@{LeaveDate=$date.ToString("yyyy-MM-dd");LeaveTypeCode=$LeaveTypeCode;IsHalfDay=$false;HalfDayOption=$null})
 ApprovalSelections=@(@{Level=1;ApproverCode=$Approver1EmployeeCode},@{Level=2;ApproverCode=$Approver2EmployeeCode})}
 $created=Invoke-Api POST "/api/LeaveDays/create" $requesterToken $body
 Add-Result "Submit registration" ($created.StatusCode -eq 200 -and $created.Json.IsSuccess -eq $true) "POST /api/LeaveDays/create"
 $id=[int](Invoke-SqlScalar "SELECT TOP(1) Id FROM dbo.F03LeaveDays WHERE EmployeeCode=@employee AND LeaveReason=@reason ORDER BY Id DESC" @{employee=$RequesterEmployeeCode;reason=$reason})
 if($id -le 0){throw "Could not resolve created request ID."}
 Add-Result "SQL request persisted" $true "dbo.F03LeaveDays.Id=$id; marker=$RunId"

 $snapshotCount=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03ApprovalSnapshots WHERE RequestId=@id" @{id=$id})
 Add-Result "Approval snapshot persisted" ($snapshotCount -eq 1) "snapshot count=$snapshotCount"

 $pending1=Invoke-Api GET "/api/LeaveDays/pending" $approver1Token
 Add-Result "Level 1 inbox" ($pending1.StatusCode -eq 200 -and $pending1.Content.Contains([string]$id)) "Request appears for first approver"
 $a1=Invoke-Api POST "/api/LeaveDays/approve" $approver1Token @{Ids=@($id);Level=1;Comment="$RunId level 1"}
 Add-Result "Approve level 1" ($a1.StatusCode -eq 200 -and $a1.Json.IsSuccess -eq $true) "First decision accepted"

 $pending2=Invoke-Api GET "/api/LeaveDays/pending" $approver2Token
 Add-Result "Level 2 inbox" ($pending2.StatusCode -eq 200 -and $pending2.Content.Contains([string]$id)) "Request appears for second approver after level 1"
 $a2=Invoke-Api POST "/api/LeaveDays/approve" $approver2Token @{Ids=@($id);Level=2;Comment="$RunId level 2"}
 Add-Result "Approve level 2" ($a2.StatusCode -eq 200 -and $a2.Json.IsSuccess -eq $true) "Final decision accepted"

 $details=Invoke-Api GET "/api/LeaveDays/details/$id" $requesterToken
 $finalStatus=[int](Invoke-SqlScalar "SELECT RequestStatus FROM dbo.F03LeaveDays WHERE Id=@id" @{id=$id})
 Add-Result "Requester reads final result" ($details.StatusCode -eq 200 -and $finalStatus -eq 3) "Details endpoint status=$($details.StatusCode); SQL RequestStatus=$finalStatus (Approved=3)"

 $historyExists=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM sys.tables WHERE name='F03ApprovalHistories'")
 if($historyExists -gt 0){
  $historyCount=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03ApprovalHistories WHERE RequestId=@id" @{id=$id})
  Add-Result "SQL approval history" ($historyCount -eq 2) "history rows=$historyCount"
 }else{throw "BLOCKED: approval history table dbo.F03ApprovalHistories was not found; map the deployed EF table name before accepting this run."}

 $approverQueue=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03EmailQueues WHERE CreatedAt>=@started AND ToEmail=@email AND (Payload LIKE @marker OR Body LIKE @marker OR Subject LIKE @marker)" @{started=$startedAt.ToString("s");email=$Approver1Email;marker="%$id%"})
 $requesterQueue=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03EmailQueues WHERE CreatedAt>=@started AND ToEmail=@email AND (Payload LIKE @marker OR Body LIKE @marker OR Subject LIKE @marker)" @{started=$startedAt.ToString("s");email=$RequesterEmail;marker="%$RunId%"})
 Add-Result "Approval email queued" ($approverQueue -ge 1) "Captured email queue rows to level 1=$approverQueue"
 Add-Result "Final result email queued" ($requesterQueue -ge 1) "Captured email queue rows to requester=$requesterQueue"

 $out=Join-Path (Get-Location) "TestResults";New-Item -ItemType Directory -Force -Path $out|Out-Null
 $report=[pscustomobject]@{RunId=$RunId;StartedAt=$startedAt.ToString("o");FinishedAt=(Get-Date).ToString("o");ApiBaseUrl=$ApiBaseUrl;Database=$db.InitialCatalog;RequestId=$id;Overall="PASS";Results=$results}
 $path=Join-Path $out "$RunId.json";$report|ConvertTo-Json -Depth 20|Set-Content $path -Encoding UTF8
 Write-Host "E2E PASS. RequestId=$id; evidence=$path" -ForegroundColor Green
}catch{
 $out=Join-Path (Get-Location) "TestResults";New-Item -ItemType Directory -Force -Path $out|Out-Null
 $report=[pscustomobject]@{RunId=$RunId;StartedAt=$startedAt.ToString("o");FinishedAt=(Get-Date).ToString("o");Error=$_.Exception.Message;Overall="FAIL_OR_BLOCKED";Results=$results}
 $path=Join-Path $out "$RunId.json";$report|ConvertTo-Json -Depth 20|Set-Content $path -Encoding UTF8
 Write-Error "$($_.Exception.Message) Evidence: $path";exit 1
} -and $null -ne $prop.Value -and [string]$prop.Value -eq [string]$RequestId) { return $true }
   if (Test-ContainsExactRequestId $prop.Value $RequestId) { return $true }
  }
 }
 return $false
}
function Add-Result([string]$Name,[bool]$Passed,[string]$Detail) {
 $results.Add([pscustomobject]@{Test=$Name;Result=$(if($Passed){"PASS"}else{"FAIL"});Detail=$Detail})
 Write-Host ("[{0}] {1}: {2}" -f $(if($Passed){"PASS"}else{"FAIL"}),$Name,$Detail)
 if(-not $Passed){throw "Assertion failed: $Name - $Detail"}
}
function Invoke-Api([string]$Method,[string]$Path,[string]$Token,$Body=$null) {
 $headers=@{}; if($Token){$headers.Authorization="Bearer $Token"}
 $args=@{Method=$Method;Uri=($ApiBaseUrl.TrimEnd("/")+$Path);Headers=$headers;ContentType="application/json";ErrorAction="Stop"}
 if($null -ne $Body){$args.Body=($Body|ConvertTo-Json -Depth 20 -Compress)}
 try {$r=Invoke-WebRequest @args; $j=$null;if($r.Content){$j=$r.Content|ConvertFrom-Json};return [pscustomobject]@{StatusCode=[int]$r.StatusCode;Json=$j;Content=$r.Content}}
 catch { $status=0;$bodyText="";if($_.Exception.Response){try{$status=[int]$_.Exception.Response.StatusCode}catch{};try{$sr=[IO.StreamReader]::new($_.Exception.Response.GetResponseStream());$bodyText=$sr.ReadToEnd();$sr.Dispose()}catch{}};throw "HTTP $Method $Path failed status=$status body=$bodyText $($_.Exception.Message)" }
}
function Get-Token($Response) {
 $json = $Response.Content
 if ($json -match '(?i)"token"\s*:\s*"([^"]+)"') { return [string]$Matches[1] }
 throw "No token in login response: $($Response.Content)"
}
function Invoke-SqlScalar([string]$Query,[hashtable]$Parameters=@{}) {
 $c=[System.Data.SqlClient.SqlConnection]::new($SqlConnectionString)
 try{$c.Open();$cmd=$c.CreateCommand();$cmd.CommandText=$Query;$cmd.CommandTimeout=30
 foreach($k in $Parameters.Keys){$p=$cmd.Parameters.Add("@$k",[System.Data.SqlDbType]::NVarChar,4000);$p.Value=[string]$Parameters[$k]}
 return $cmd.ExecuteScalar()
 }finally{if($c.State -ne "Closed"){$c.Close()};$c.Dispose()}
}
try {
 $required=@{
 E2E_API_BASE_URL=$ApiBaseUrl;E2E_SQL_CONNECTION_STRING=$SqlConnectionString;E2E_LEAVE_TYPE_CODE=$LeaveTypeCode
 E2E_REQUESTER_USERNAME=$RequesterUserName;E2E_REQUESTER_PASSWORD=$RequesterPassword;E2E_REQUESTER_EMPLOYEE_CODE=$RequesterEmployeeCode;E2E_REQUESTER_EMAIL=$RequesterEmail
 E2E_APPROVER1_USERNAME=$Approver1UserName;E2E_APPROVER1_PASSWORD=$Approver1Password;E2E_APPROVER1_EMPLOYEE_CODE=$Approver1EmployeeCode;E2E_APPROVER1_EMAIL=$Approver1Email
 E2E_APPROVER2_USERNAME=$Approver2UserName;E2E_APPROVER2_PASSWORD=$Approver2Password;E2E_APPROVER2_EMPLOYEE_CODE=$Approver2EmployeeCode
 }
 foreach($k in $required.Keys){Assert-Config $k $required[$k]}
 if($env:E2E_ALLOW_RUN -ne "YES"){throw "Safety stop: set E2E_ALLOW_RUN=YES only for a disposable test environment."}
 $db=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($SqlConnectionString)
 if($db.InitialCatalog -notmatch '(?i)(test|qa|e2e|automation)'){throw "Safety stop: database '$($db.InitialCatalog)' must contain Test, QA, E2E, or Automation."}
 if($ApiBaseUrl -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?(/|$)'){throw "Safety stop: first-run runner only permits localhost API."}

 # Refuse to run when active/default dispatch profiles could send to real SMTP.
 $fakePort = 1025
 if($env:E2E_FAKE_SMTP_PORT){$fakePort=[int]$env:E2E_FAKE_SMTP_PORT}
 $unsafeProfiles = [int](Invoke-SqlScalar @"
SELECT COUNT(*)
FROM dbo.F03EmailProfiles p
WHERE p.IsActive = 1
  AND (p.IsDefault = 1 OR EXISTS (
      SELECT 1 FROM dbo.F03EmailDispatchPolicies d
      WHERE d.IsActive = 1 AND d.EmailProfileCode = p.Code
  ))
  AND NOT (
      LOWER(p.EmailServerName) IN ('localhost','127.0.0.1','::1','host.docker.internal')
      OR LOWER(p.EmailServerName) LIKE '%mailpit%'
      OR LOWER(p.EmailServerName) LIKE '%smtp4dev%'
  );
"@)
 if($unsafeProfiles -gt 0){throw "Safety stop: $unsafeProfiles active/default email profile(s) point outside a local SMTP capture host. Configure Mailpit/smtp4dev in the disposable test DB before running."}
 $wrongPorts = [int](Invoke-SqlScalar @"
SELECT COUNT(*)
FROM dbo.F03EmailProfiles p
WHERE p.IsActive = 1
  AND (p.IsDefault = 1 OR EXISTS (
      SELECT 1 FROM dbo.F03EmailDispatchPolicies d
      WHERE d.IsActive = 1 AND d.EmailProfileCode = p.Code
  ))
  AND p.EmailServerPort <> @port;
"@ @{port="$fakePort"})
 if($wrongPorts -gt 0){throw "Safety stop: active/default email profile port does not match E2E_FAKE_SMTP_PORT=$fakePort. Configure the local capture service."}

 # Authenticate all three actors. This also confirms the API is reachable.
 $loginBody={param($u,$p) @{UserName=$u;Password=$p;DeviceId=[guid]::NewGuid().ToString();DeviceType="E2E";DeviceName="E2E Regression";RememberMe=$false}}
 $requesterToken=Get-Token (Invoke-Api POST "/api/Auth/login" "" (&$loginBody $RequesterUserName $RequesterPassword))
 $approver1Token=Get-Token (Invoke-Api POST "/api/Auth/login" "" (&$loginBody $Approver1UserName $Approver1Password))
 $approver2Token=Get-Token (Invoke-Api POST "/api/Auth/login" "" (&$loginBody $Approver2UserName $Approver2Password))
 Add-Result "Login requester + approvers" $true "All three accounts authenticated"

 $date=(Get-Date).Date.AddDays(14);while($date.DayOfWeek -in @([DayOfWeek]::Saturday,[DayOfWeek]::Sunday)){$date=$date.AddDays(1)}
 $reason="$RunId approval-email E2E"
 $body=@{Id=0;StartDate=$date.ToString("yyyy-MM-dd");EndDate=$date.ToString("yyyy-MM-dd");LeaveTypeCode=$LeaveTypeCode;Reason=$reason
 Details=@(@{LeaveDate=$date.ToString("yyyy-MM-dd");LeaveTypeCode=$LeaveTypeCode;IsHalfDay=$false;HalfDayOption=$null})
 ApprovalSelections=@(@{Level=1;ApproverCode=$Approver1EmployeeCode},@{Level=2;ApproverCode=$Approver2EmployeeCode})}
 $created=Invoke-Api POST "/api/LeaveDays/create" $requesterToken $body
 Add-Result "Submit registration" ($created.StatusCode -eq 200 -and $created.Json.IsSuccess -eq $true) "POST /api/LeaveDays/create"
 $id=[int](Invoke-SqlScalar "SELECT TOP(1) Id FROM dbo.F03LeaveDays WHERE EmployeeCode=@employee AND LeaveReason=@reason ORDER BY Id DESC" @{employee=$RequesterEmployeeCode;reason=$reason})
 if($id -le 0){throw "Could not resolve created request ID."}
 Add-Result "SQL request persisted" $true "dbo.F03LeaveDays.Id=$id; marker=$RunId"

 $snapshotCount=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03ApprovalSnapshots WHERE RequestId=@id" @{id=$id})
 Add-Result "Approval snapshot persisted" ($snapshotCount -eq 1) "snapshot count=$snapshotCount"

 $pending1=Invoke-Api GET "/api/LeaveDays/pending" $approver1Token
 Add-Result "Level 1 inbox" ($pending1.StatusCode -eq 200 -and $pending1.Content.Contains([string]$id)) "Request appears for first approver"
 $a1=Invoke-Api POST "/api/LeaveDays/approve" $approver1Token @{Ids=@($id);Level=1;Comment="$RunId level 1"}
 Add-Result "Approve level 1" ($a1.StatusCode -eq 200 -and $a1.Json.IsSuccess -eq $true) "First decision accepted"

 $pending2=Invoke-Api GET "/api/LeaveDays/pending" $approver2Token
 Add-Result "Level 2 inbox" ($pending2.StatusCode -eq 200 -and $pending2.Content.Contains([string]$id)) "Request appears for second approver after level 1"
 $a2=Invoke-Api POST "/api/LeaveDays/approve" $approver2Token @{Ids=@($id);Level=2;Comment="$RunId level 2"}
 Add-Result "Approve level 2" ($a2.StatusCode -eq 200 -and $a2.Json.IsSuccess -eq $true) "Final decision accepted"

 $details=Invoke-Api GET "/api/LeaveDays/details/$id" $requesterToken
 $finalStatus=[int](Invoke-SqlScalar "SELECT RequestStatus FROM dbo.F03LeaveDays WHERE Id=@id" @{id=$id})
 Add-Result "Requester reads final result" ($details.StatusCode -eq 200 -and $finalStatus -eq 3) "Details endpoint status=$($details.StatusCode); SQL RequestStatus=$finalStatus (Approved=3)"

 $historyExists=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM sys.tables WHERE name='F03ApprovalHistories'")
 if($historyExists -gt 0){
  $historyCount=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03ApprovalHistories WHERE RequestId=@id" @{id=$id})
  Add-Result "SQL approval history" ($historyCount -eq 2) "history rows=$historyCount"
 }else{throw "BLOCKED: approval history table dbo.F03ApprovalHistories was not found; map the deployed EF table name before accepting this run."}

 $approverQueue=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03EmailQueues WHERE CreatedAt>=@started AND ToEmail=@email AND (Payload LIKE @marker OR Body LIKE @marker OR Subject LIKE @marker)" @{started=$startedAt.ToString("s");email=$Approver1Email;marker="%$id%"})
 $requesterQueue=[int](Invoke-SqlScalar "SELECT COUNT(*) FROM dbo.F03EmailQueues WHERE CreatedAt>=@started AND ToEmail=@email AND (Payload LIKE @marker OR Body LIKE @marker OR Subject LIKE @marker)" @{started=$startedAt.ToString("s");email=$RequesterEmail;marker="%$RunId%"})
 Add-Result "Approval email queued" ($approverQueue -ge 1) "Captured email queue rows to level 1=$approverQueue"
 Add-Result "Final result email queued" ($requesterQueue -ge 1) "Captured email queue rows to requester=$requesterQueue"

 $out=Join-Path (Get-Location) "TestResults";New-Item -ItemType Directory -Force -Path $out|Out-Null
 $report=[pscustomobject]@{RunId=$RunId;StartedAt=$startedAt.ToString("o");FinishedAt=(Get-Date).ToString("o");ApiBaseUrl=$ApiBaseUrl;Database=$db.InitialCatalog;RequestId=$id;Overall="PASS";Results=$results}
 $path=Join-Path $out "$RunId.json";$report|ConvertTo-Json -Depth 20|Set-Content $path -Encoding UTF8
 Write-Host "E2E PASS. RequestId=$id; evidence=$path" -ForegroundColor Green
}catch{
 $out=Join-Path (Get-Location) "TestResults";New-Item -ItemType Directory -Force -Path $out|Out-Null
 $report=[pscustomobject]@{RunId=$RunId;StartedAt=$startedAt.ToString("o");FinishedAt=(Get-Date).ToString("o");Error=$_.Exception.Message;Overall="FAIL_OR_BLOCKED";Results=$results}
 $path=Join-Path $out "$RunId.json";$report|ConvertTo-Json -Depth 20|Set-Content $path -Encoding UTF8
 Write-Error "$($_.Exception.Message) Evidence: $path";exit 1
}