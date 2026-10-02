param(
    [string]$Root = "FVN_REGISTER.Shared",
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
$files = Get-ChildItem -Path $Root -Recurse -File | Where-Object { $_.Extension -in '.razor','.cs' }
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
