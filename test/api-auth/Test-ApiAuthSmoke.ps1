#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaseUrl,
    [string]$InventoryPath = "./artifacts/api-auth/endpoint-inventory.csv",
    [string]$OutputDirectory = "./artifacts/api-auth",
    [int]$TimeoutSeconds = 15,
    # Allow a non-local target (e.g. the Develop server). Production hosts must never be used.
    [switch]$AllowRemote
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Sends NO credentials. Only GET requests to endpoints marked Authenticated and without route parameters.
# Expectation: 401 (token missing). 200/2xx would mean the endpoint is reachable anonymously -> FAIL.
# 404/405 are reported as INCONCLUSIVE (route/verb mismatch), 5xx as ERROR. No data is modified.

$uri = [Uri]$BaseUrl
$isLocal = $uri.Host -in @("localhost", "127.0.0.1", "::1")
if (-not $isLocal -and -not $AllowRemote) { throw "Refusing non-local BaseUrl '$($uri.Host)'. Pass -AllowRemote for the Develop server." }
if ($uri.Scheme -notin @("http", "https")) { throw "BaseUrl must be http or https." }
if (-not (Test-Path $InventoryPath)) { throw "Inventory not found: $InventoryPath. Run Get-EndpointInventory.ps1 first." }
$null = New-Item -ItemType Directory -Force -Path $OutputDirectory

$targets = @(Import-Csv -Encoding UTF8 $InventoryPath | Where-Object {
    $_.Access -eq "Authenticated" -and $_.Verb -eq "GET" -and $_.HasRouteParameters -ne "True"
})
Write-Host "Probing $($targets.Count) GET endpoints on $($uri.GetLeftPart('Authority')) without credentials."

$results = [System.Collections.Generic.List[object]]::new()
foreach ($t in $targets) {
    $url = "$($uri.GetLeftPart('Authority'))/$($t.Route.TrimStart('/'))"
    $code = 0; $note = ""
    try {
        $resp = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec $TimeoutSeconds -SkipHttpErrorCheck -MaximumRedirection 0 -ErrorAction Stop
        $code = [int]$resp.StatusCode
    } catch { $code = -1; $note = $_.Exception.Message }
    $status = switch ($code) {
        401 { "PASS" }
        { $_ -ge 200 -and $_ -lt 300 } { "FAIL" }
        { $_ -in 301, 302, 307, 308 } { "FAIL" }   # redirect to login is not an API 401
        403 { "WARN" }                               # rejected, but before authentication was expected
        { $_ -in 404, 405 } { "INCONCLUSIVE" }
        -1 { "ERROR" }
        default { if ($code -ge 500) { "ERROR" } else { "WARN" } }
    }
    $results.Add([pscustomobject]@{ Verb = "GET"; Route = $t.Route; Controller = $t.Controller; StatusCode = $code; Result = $status; Note = $note })
}

$results | Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $OutputDirectory "api-auth-smoke.csv")
$count = { param($s) @($results | Where-Object Result -eq $s).Count }
$fail = & $count "FAIL"; $err = & $count "ERROR"; $warn = & $count "WARN"; $inc = & $count "INCONCLUSIVE"; $pass = & $count "PASS"

$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# API anonymous smoke - $(if ($fail -gt 0) { 'FAIL' } elseif ($err -gt 0 -or $pass -eq 0) { 'ERROR' } else { 'PASS' })"); $md.Add("")
$md.Add("Target: $($uri.GetLeftPart('Authority')) | PASS $pass, FAIL $fail, WARN $warn, INCONCLUSIVE $inc, ERROR $err of $($results.Count)"); $md.Add("")
foreach ($r in @($results | Where-Object { $_.Result -in @("FAIL", "WARN", "ERROR") })) { $md.Add("- **$($r.Result)** GET /$($r.Route) -> $($r.StatusCode) $($r.Note)") }
$md | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "api-auth-smoke.md")
if ($env:GITHUB_STEP_SUMMARY) { $md | Add-Content -Encoding utf8 $env:GITHUB_STEP_SUMMARY }

Write-Host "PASS=$pass FAIL=$fail WARN=$warn INCONCLUSIVE=$inc ERROR=$err"
# ERROR / zero PASS means the API was not really reachable: do not report a green run.
if ($fail -gt 0 -or $err -gt 0 -or $pass -eq 0) { exit 1 }
