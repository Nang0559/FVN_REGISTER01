[CmdletBinding()]
param(
    [string]$ControllersDirectory = "./FVN_REGISTER.API/Controllers",
    [string]$OutputDirectory = "./artifacts/api-auth",
    # Fail when an endpoint is neither [Authorize] nor [AllowAnonymous] (implicitly open: there is no fallback policy).
    [switch]$FailOnImplicitlyOpen,
    # Optional allow-list of anonymous endpoints, one "VERB route" per line (e.g. "POST api/auth/login").
    [string]$AnonymousAllowListPath = ""
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
# Static analysis only: reads *.cs files, no build, no network, no database.

$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
$files = Get-ChildItem -Path $ControllersDirectory -Filter *Controller.cs -File
$rows = [System.Collections.Generic.List[object]]::new()

function Join-Route([string]$a, [string]$b) {
    $parts = @($a, $b) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim('/') }
    return ($parts -join '/')
}

foreach ($f in $files) {
    $text = Get-Content -Raw -Encoding UTF8 $f.FullName
    $m = [regex]::Match($text, '(?:public|internal)\s+(?:sealed\s+|abstract\s+|partial\s+)*class\s+(\w+)')
    if (-not $m.Success) { continue }
    $className = $m.Groups[1].Value
    if ($className -eq "BaseApiController") { continue }
    # Class attributes = everything between the previous statement/brace and the class keyword
    # (attribute arguments such as Route("api/[controller]") contain ']' so we do not parse brackets here).
    $before = $text.Substring(0, $m.Index)
    $cut = $before.LastIndexOfAny([char[]]@(';', '{', '}'))
    $classAttrs = if ($cut -ge 0) { $before.Substring($cut + 1) } else { $before }
    $ctrlName = $className -replace 'Controller$', ''
    $rm = [regex]::Match($classAttrs, 'Route\(\s*"([^"]*)"')
    $classRoute = if ($rm.Success) { $rm.Groups[1].Value -replace '\[controller\]', $ctrlName.ToLower() } else { $ctrlName.ToLower() }
    $classAuth = $classAttrs -match '\bAuthorize\b'
    $classAnon = $classAttrs -match '\bAllowAnonymous\b'

    $body = $text.Substring($m.Index + $m.Length)
    $pubIdx = @([regex]::Matches($body, '\bpublic\s') | ForEach-Object { $_.Index })
    foreach ($h in [regex]::Matches($body, '\bHttp(Get|Post|Put|Delete|Patch)(?:\(\s*(?:"([^"]*)")?[^)]*\))?')) {
        $prev = @($pubIdx | Where-Object { $_ -lt $h.Index }) | Select-Object -Last 1
        $next = @($pubIdx | Where-Object { $_ -gt $h.Index }) | Select-Object -First 1
        if ($null -eq $prev) { $prev = 0 }
        if ($null -eq $next) { $next = [Math]::Min($body.Length, $h.Index + 400) }
        $window = $body.Substring($prev, $next - $prev)
        # strip the previous member's signature/body noise: only keep attribute-looking text
        $attrText = ([regex]::Matches($window, '\[[^\]\r\n]*\]') | ForEach-Object { $_.Value }) -join ' '
        $methAnon = $attrText -match '\bAllowAnonymous\b'
        $methAuth = $attrText -match '\bAuthorize\b'
        $route = Join-Route $classRoute $h.Groups[2].Value
        $effective = if ($methAnon -or ($classAnon -and -not $methAuth)) { "Anonymous" }
                     elseif ($methAuth -or $classAuth) { "Authenticated" }
                     else { "ImplicitlyOpen" }
        $hasParams = $route -match '\{[^}]+\}'
        $rows.Add([pscustomobject]@{
            Controller = $className; Verb = $h.Groups[1].Value.ToUpper(); Route = $route
            Access = $effective; HasRouteParameters = $hasParams; File = $f.Name
        })
    }
}

$rows | Sort-Object Controller, Route, Verb | Export-Csv -NoTypeInformation -Encoding utf8 (Join-Path $OutputDirectory "endpoint-inventory.csv")
$rows | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "endpoint-inventory.json")

$allow = @()
if ($AnonymousAllowListPath -and (Test-Path $AnonymousAllowListPath)) {
    $allow = @(Get-Content -Encoding UTF8 $AnonymousAllowListPath | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { $_.Trim().ToLower() })
}
$anon = @($rows | Where-Object Access -eq "Anonymous")
$open = @($rows | Where-Object Access -eq "ImplicitlyOpen")
$unlisted = @(if ($allow.Count -gt 0) { $anon | Where-Object { "$($_.Verb) $($_.Route)".ToLower() -notin $allow } })

$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# API endpoint authentication inventory"); $md.Add("")
$md.Add("- Controllers: **$(@($rows | Select-Object -ExpandProperty Controller -Unique).Count)**, endpoints: **$($rows.Count)**")
$md.Add("- Authenticated: **$(@($rows | Where-Object Access -eq 'Authenticated').Count)**, Anonymous: **$($anon.Count)**, Implicitly open (no attribute): **$($open.Count)**")
$md.Add("- Note: fine-grained authorization is enforced in code (e.g. IAuthorizationService.HasAsync); this inventory only covers the authentication layer.")
$md.Add("")
if ($open.Count -gt 0) { $md.Add("## Implicitly open endpoints (no [Authorize] / [AllowAnonymous])"); $md.Add(""); foreach ($r in $open) { $md.Add("- $($r.Verb) /$($r.Route) ($($r.Controller))") }; $md.Add("") }
$md.Add("## Anonymous endpoints (review each)"); $md.Add("")
foreach ($r in $anon) { $md.Add("- $($r.Verb) /$($r.Route) ($($r.Controller))") }
if ($unlisted.Count -gt 0) { $md.Add(""); $md.Add("## Anonymous endpoints NOT in the allow-list"); $md.Add(""); foreach ($r in $unlisted) { $md.Add("- $($r.Verb) /$($r.Route)") } }
$md | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "endpoint-inventory.md")
if ($env:GITHUB_STEP_SUMMARY) { $md | Add-Content -Encoding utf8 $env:GITHUB_STEP_SUMMARY }

Write-Host "Endpoints: $($rows.Count); anonymous: $($anon.Count); implicitly open: $($open.Count); anonymous not in allow-list: $($unlisted.Count)"
if (($FailOnImplicitlyOpen -and $open.Count -gt 0) -or $unlisted.Count -gt 0) { exit 1 }
