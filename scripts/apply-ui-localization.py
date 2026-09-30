from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SHARED = ROOT / 'FVN_REGISTER.Shared'
LANG_DIR = SHARED / 'Services' / 'Language'

PAIR_RE = re.compile(r'\["(?P<key>[^"]+)"\]\s*=\s*"(?P<value>(?:\\.|[^"\\])*)"')
ATTR_RE = re.compile(r'(?P<attr>\b(?:Label|Text|Title|Placeholder|HelperText|ToolTip|Tooltip|AriaLabel|Description))="(?P<value>[^"\r\n]+)"')
TEXT_RE = re.compile(r'>(?P<text>[^<>@\r\n]+)<')

mapping: dict[str, str] = {}
for path in LANG_DIR.glob('LanguageCatalog*.cs'):
    text = path.read_text(encoding='utf-8-sig')
    for m in PAIR_RE.finditer(text):
        key = m.group('key')
        value = bytes(m.group('value'), 'utf-8').decode('unicode_escape')
        if value and value not in mapping:
            mapping[value] = key

if not mapping:
    raise SystemExit('No localization catalog entries found.')

changed = []
for path in SHARED.rglob('*.razor'):
    if any(part in {'bin', 'obj'} for part in path.parts):
        continue
    original = path.read_text(encoding='utf-8-sig')

    def attr_replace(m: re.Match[str]) -> str:
        value = m.group('value').strip()
        key = mapping.get(value)
        if not key or value.startswith('@'):
            return m.group(0)
        return f"{m.group('attr')}='@Language.T(\"{key}\")'"

    content = ATTR_RE.sub(attr_replace, original)

    lines = content.splitlines(keepends=True)
    in_code = 0
    for i, line in enumerate(lines):
        if re.match(r'^\s*@code\s*\{', line):
            in_code = 1
            continue
        if in_code:
            in_code += line.count('{') - line.count('}')
            if in_code <= 0:
                in_code = 0
            continue
        if '@Language.T(' in line:
            continue
        if re.match(r'^\s*(//|@\{|@if|@foreach|@switch|@for|@while|@try|@catch|@finally|var\s|private\s|public\s|protected\s|return\s)', line):
            continue

        def text_replace(m: re.Match[str]) -> str:
            raw = m.group('text')
            text = raw.strip()
            key = mapping.get(text)
            if not key:
                return m.group(0)
            leading = raw[:raw.find(text)]
            trailing = raw[raw.find(text) + len(text):]
            return f">{leading}@Language.T(\"{key}\"){trailing}<"

        lines[i] = TEXT_RE.sub(text_replace, line)

    content = ''.join(lines)
    if content != original:
        path.write_text(content, encoding='utf-8', newline='')
        changed.append(path.relative_to(ROOT).as_posix())

print(f'Localization migration: {len(changed)} Razor files changed; {len(mapping)} catalog entries available.')
for item in changed:
    print(item)
