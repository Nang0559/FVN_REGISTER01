param(
    [string]$Root = "",
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
$roots = if ([string]::IsNullOrWhiteSpace($Root)) {
    @("FVN_REGISTER.Shared", "FVN_REGISTER.Web")
} else {
    @($Root)
}
$files = foreach ($rootPath in $roots) {
    if (Test-Path -LiteralPath $rootPath) {
        Get-ChildItem -Path $rootPath -Recurse -File | Where-Object { $_.Extension -in '.razor','.cs' }
    }
}
$patterns = @(
    'MudButton[^>]*>\s*([^<@][^<]*)<',
    'Label\s*=\s*"([^"]+)"',
    'Placeholder\s*=\s*"([^"]+)"',
    'Title\s*=\s*"([^"]+)"',
    'Text\s*=\s*"([^"]+)"',
    'Snackbar\.Add\(\s*"([^"]+)"',
    'Message\s*=\s*"([^"]+)"',
    'PageTitle>\s*([^<@][^<]*)<',
    'MudMenuItem[^>]*>\s*([^<@][^<]*)<'
)

$hits = foreach ($file in $files) {
    $text = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($pattern in $patterns) {
        foreach ($match in [regex]::Matches($text, $pattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
            $value = $match.Groups[1].Value.Trim()
            if ($value -and $value -notmatch '^[@{]' -and $value -notmatch '^[A-Za-z0-9_./:-]+$') {
                [pscustomobject]@{ File = $file.FullName; Text = $value }
            }
        }
    }
}

$hits = $hits | Sort-Object File, Text -Unique
$hits | Format-Table -AutoSize
Write-Host "Potential UI literals: $($hits.Count)"

if ($Strict -and $hits.Count -gt 0) {
    throw "Localization audit found $($hits.Count) potential UI literals. Review each result; business/data literals must be explicitly classified before completion."
}
