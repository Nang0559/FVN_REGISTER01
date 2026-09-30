$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sharedRoot = Join-Path $repoRoot 'FVN_REGISTER.Shared'

# Build a reverse lookup from the canonical Vietnamese catalogs already committed in the branch.
$catalogFiles = Get-ChildItem -Path (Join-Path $sharedRoot 'Services/Language') -Filter 'LanguageCatalog*.cs' -File
$map = [ordered]@{}

foreach ($file in $catalogFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($m in [regex]::Matches($text, '\["(?<key>[^"]+)"\]\s*=\s*"(?<value>(?:\\.|[^"\\])*)"')) {
        $key = $m.Groups['key'].Value
        $value = $m.Groups['value'].Value.Replace('\\"','"').Replace('\\n',"`n")
        if ($value -and -not $map.Contains($value)) { $map[$value] = $key }
    }
}

if ($map.Count -eq 0) { throw 'No Vietnamese localization entries were discovered.' }

$files = Get-ChildItem -Path $sharedRoot -Recurse -Filter '*.razor' -File |
    Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]|[\\/]bin[\\/]' }

$changed = 0
foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $original = $content

    # Attribute literals used by MudBlazor/HTML UI controls.
    $content = [regex]::Replace($content, '(?<attr>\b(?:Label|Text|Title|Placeholder|HelperText|ToolTip|Tooltip|AriaLabel|Description))="(?<value>[^"\r\n]+)"', {
        param($m)
        $value = $m.Groups['value'].Value.Trim()
        if ($map.Contains($value) -and $value -notmatch '^@') {
            "{0}='@Language.T(\"{1}\")'" -f $m.Groups['attr'].Value, $map[$value]
        } else { $m.Value }
    })

    # Exact plain text nodes in markup. Keep code blocks and dynamic expressions untouched.
    $lines = $content -split "`r?`n", -1
    $inCode = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s*@code\s*\{') { $inCode = 1; continue }
        if ($inCode -gt 0) {
            $open = ([regex]::Matches($line, '\{')).Count
            $close = ([regex]::Matches($line, '\}')).Count
            $inCode += $open - $close
            if ($inCode -le 0) { $inCode = 0 }
            continue
        }
        if ($line -match '@Language\.T\(') { continue }
        if ($line -match '^\s*(//|@\{|@if|@foreach|@switch|@for|@while|@try|@catch|@finally|var\s|private\s|public\s|protected\s|return\s|[A-Za-z_][A-Za-z0-9_<>?\[\]]*\s+[A-Za-z_][A-Za-z0-9_]*\s*=)') { continue }

        $lines[$i] = [regex]::Replace($line, '>(?<text>[^<>@\r\n]+)<', {
            param($m)
            $text = $m.Groups['text'].Value.Trim()
            if ($text.Length -gt 0 -and $map.Contains($text)) {
                $leading = $m.Groups['text'].Value.Substring(0, $m.Groups['text'].Value.IndexOf($text))
                $trailing = $m.Groups['text'].Value.Substring($m.Groups['text'].Value.IndexOf($text) + $text.Length)
                ">${leading}@Language.T(\"$($map[$text])\")${trailing}<"
            } else { $m.Value }
        })
    }
    $content = $lines -join "`n"

    if ($content -ne $original) {
        Set-Content -LiteralPath $file.FullName -Value $content -Encoding UTF8 -NoNewline
        $changed++
        Write-Host "Localized: $($file.FullName.Substring($repoRoot.Length + 1))"
    }
}

Write-Host "Localization migration completed. Changed $changed Razor files using $($map.Count) catalog entries."
