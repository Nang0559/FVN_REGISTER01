#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$BaseUrl,[string]$MatrixPath="./test/api-auth/role-access-matrix.json",[string]$OutputDirectory="./artifacts/api-auth",[int]$TimeoutSeconds=20,[switch]$AllowRemote)
Set-StrictMode -Version Latest
$ErrorActionPreference="Stop"
$uri=[Uri]$BaseUrl
if($uri.Scheme -notin @("http","https")){throw "BaseUrl must be http or https."}
if($uri.Host -notin @("localhost","127.0.0.1","::1") -and -not $AllowRemote){throw "Refusing non-local host; pass -AllowRemote for Develop."}
$origin=$uri.GetLeftPart('Authority')
$null=New-Item -ItemType Directory -Force -Path $OutputDirectory
$matrix=Get-Content -Raw -Encoding UTF8 $MatrixPath|ConvertFrom-Json
function Get-Status([string]$Route,[string]$Token){$h=@{};if($Token){$h.Authorization="Bearer $Token"};try{[int](Invoke-WebRequest -Uri "$origin/$($Route.TrimStart('/'))" -Method Get -Headers $h -TimeoutSec $TimeoutSeconds -SkipHttpErrorCheck -MaximumRedirection 0).StatusCode}catch{-1}}
function Get-LoginToken([string]$User,[string]$Pass){
 $body=@{UserName=$User;Password=$Pass;DeviceId=[guid]::NewGuid().ToString();DeviceType="RoleTest";DeviceName="Role access test";RememberMe=$false}|ConvertTo-Json
 try{$r=Invoke-WebRequest -Uri "$origin/api/Auth/login" -Method Post -ContentType "application/json" -Body $body -TimeoutSec $TimeoutSeconds -SkipHttpErrorCheck;$t=if($r.Content -is [byte[]]){[Text.Encoding]::UTF8.GetString($r.Content)}else{[string]$r.Content};if($t -match '(?i)"token"\s*:\s*"([^"]+)"'){return [string]$Matches[1]}}catch{}
 return $null
}
$results=[System.Collections.Generic.List[object]]::new()
function Add-Result($Role,$Route,$Expect,$Code,$Result,$Detail=""){$results.Add([pscustomobject]@{Role=$Role;Route=$Route;Expect=$Expect;StatusCode=$Code;Result=$Result;Detail=$Detail})}
foreach($e in $matrix.endpoints){$code=Get-Status $e.route "invalid.token.value";Add-Result "(invalid token)" $e.route "401" $code $(if($code -eq 401){"PASS"}elseif($code -eq -1){"ERROR"}else{"FAIL"})}
foreach($role in $matrix.roles){
 $key=$role.ToUpper();$user=[Environment]::GetEnvironmentVariable("ROLE_${key}_USERNAME");$pass=[Environment]::GetEnvironmentVariable("ROLE_${key}_PASSWORD")
 if([string]::IsNullOrWhiteSpace($user)-or[string]::IsNullOrWhiteSpace($pass)){Add-Result $role "*" "-" 0 "SKIP" "No ROLE_${key}_USERNAME/PASSWORD set";continue}
 $token=Get-LoginToken $user $pass
 if(-not $token){Add-Result $role "*" "-" 0 "BLOCKED" "Login did not return token (credentials, lockout or 2FA)";continue}
 foreach($e in $matrix.endpoints){$expect=[string]$e.expect.$role;if($expect -eq "any"-or[string]::IsNullOrEmpty($expect)){continue};$code=Get-Status $e.route $token
  $res=switch($expect){"allow"{if($code -in 401,403){"FAIL"}elseif($code -eq -1-or$code -ge 500){"ERROR"}else{"PASS"}};"deny"{if($code -eq 403){"PASS"}elseif($code -ge 200-and$code -lt 300){"FAIL"}elseif($code -eq 401){"FAIL"}elseif($code -eq -1-or$code -ge 500){"ERROR"}else{"WARN"}};default{"ERROR"}}
  Add-Result $role $e.route $expect $code $res $e.why
 }
}
$results|Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $OutputDirectory "api-role-access.csv")
$n={param($s)@($results|Where-Object Result -eq $s).Count}
$fail=&$n "FAIL";$err=&$n "ERROR";$blocked=&$n "BLOCKED";$skip=&$n "SKIP";$pass=&$n "PASS";$warn=&$n "WARN"
$roleChecked=@($results|Where-Object{$_.Role -ne "(invalid token)" -and $_.Result -in @("PASS","FAIL","WARN")}).Count
$overall=if($fail -gt 0){"FAIL"}elseif($err -gt 0-or$blocked -gt 0){"ERROR"}elseif($roleChecked -eq 0){"PASS_WITHOUT_ROLE_CHECKS"}else{"PASS"}
$md=[System.Collections.Generic.List[string]]::new();$md.Add("# API role access - $overall");$md.Add("");$md.Add("Target $origin | PASS $pass, FAIL $fail, WARN $warn, ERROR $err, BLOCKED $blocked, SKIP $skip");$md.Add("");$md.Add("| Role | Route | Expect | HTTP | Result | Detail |");$md.Add("|---|---|---|---:|---|---|")
foreach($r in $results){$md.Add("| $($r.Role) | $($r.Route) | $($r.Expect) | $($r.StatusCode) | $($r.Result) | $($r.Detail) |")}
$md|Set-Content -Encoding utf8 (Join-Path $OutputDirectory "api-role-access.md");if($env:GITHUB_STEP_SUMMARY){$md|Add-Content -Encoding utf8 $env:GITHUB_STEP_SUMMARY}
Write-Host "Role access: $overall (PASS=$pass FAIL=$fail WARN=$warn ERROR=$err BLOCKED=$blocked SKIP=$skip)"
if($overall -in @("FAIL","ERROR")){exit 1}
