from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SHARED = ROOT / "FVN_REGISTER.Shared"
LANG_DIR = SHARED / "Services" / "Language"

PAIR_RE = re.compile(r'\["(?P<key>[^"]+)"\]\s*=\s*"(?P<value>(?:\\.|[^"\\])*)"')
KEY_RE = re.compile(r'Language\.T\(\s*"([^"]+)"')
ATTR_RE = re.compile(r'\b(?:Label|Text|Title|Placeholder|HelperText|ToolTip|Tooltip|AriaLabel|Description)="(?P<value>[^"\r\n]+)"')
TEXT_RE = re.compile(r'>(?P<text>[^<>@\r\n]+)<')

catalogs: dict[str, dict[str, str]] = {"vi": {}, "ja": {}}
all_keys: dict[str, set[str]] = {"vi": set(), "ja": set()}
duplicates: list[str] = []

# Catalog files use one VI dictionary followed by one JA dictionary. Parse key/value
# pairs first, then classify by the dictionary declaration order in each file.
for path in sorted(LANG_DIR.glob("LanguageCatalog*.cs")):
    text = path.read_text(encoding="utf-8-sig")
    dictionaries = list(re.finditer(r'(?P<name>Vi|Ja)\s*=\s*new Dictionary<string, string>', text))
    if not dictionaries:
        continue
    for i, match in enumerate(dictionaries):
        language = match.group("name").lower()
        start = match.end()
        end = dictionaries[i + 1].start() if i + 1 < len(dictionaries) else len(text)
        section = text[start:end]
        for pair in PAIR_RE.finditer(section):
            key = pair.group("key")
            if key in catalogs[language]:
                duplicates.append(f"{path.relative_to(ROOT)}: duplicate {language} key {key}")
            catalogs[language][key] = pair.group("value")
            all_keys[language].add(key)

missing_ja = sorted(all_keys["vi"] - all_keys["ja"])
missing_vi = sorted(all_keys["ja"] - all_keys["vi"])

used: set[str] = set()
raw_candidates: list[str] = []

for path in sorted(SHARED.rglob("*.razor")):
    if any(part in {"bin", "obj"} for part in path.parts):
        continue
    text = path.read_text(encoding="utf-8-sig")
    used.update(KEY_RE.findall(text))

    in_code = False
    for line_no, line in enumerate(text.splitlines(), 1):
        stripped = line.strip()
        if stripped.startswith("@code"):
            in_code = True
        if in_code:
            if "}" in line and stripped.endswith("}"):
                in_code = False
            continue
        if "Language.T(" in line:
            continue
        if "@(" in line and ">" not in line and "<" not in line:
            continue
        for match in ATTR_RE.finditer(line):
            value = match.group("value").strip()
            if value and not value.startswith("@") and re.search(r"[A-Za-zÀ-ỹぁ-んァ-ヶ]", value):
                if not re.fullmatch(r"https?://.*|mailto:.*", value):
                    raw_candidates.append(f"{path.relative_to(ROOT)}:{line_no}: attribute literal: {value}")
        for match in TEXT_RE.finditer(line):
            value = match.group("text").strip()
            if not value or value.startswith("@"): continue
            if re.search(r"[A-Za-zÀ-ỹぁ-んァ-ヶ]", value) and not re.fullmatch(r"[A-Za-z0-9_./:%+\-–—→•()]+", value):
                raw_candidates.append(f"{path.relative_to(ROOT)}:{line_no}: text literal: {value}")

missing_used_vi = sorted(k for k in used if k not in all_keys["vi"])
missing_used_ja = sorted(k for k in used if k not in all_keys["ja"])

# Formatting placeholders must match across languages.
placeholder_mismatch: list[str] = []
for key in sorted(all_keys["vi"] & all_keys["ja"]):
    vi = sorted(re.findall(r"\{(\d+)\}", catalogs["vi"][key]))
    ja = sorted(re.findall(r"\{(\d+)\}", catalogs["ja"][key]))
    if vi != ja:
        placeholder_mismatch.append(f"{key}: VI={vi} JA={ja}")

print(f"Catalog keys: VI={len(all_keys['vi'])}, JA={len(all_keys['ja'])}")
print(f"Used Language.T keys: {len(used)}")
print(f"Raw UI candidates: {len(raw_candidates)}")

errors = []
errors.extend(duplicates)
errors.extend(f"missing JA: {key}" for key in missing_ja)
errors.extend(f"missing VI: {key}" for key in missing_vi)
errors.extend(f"used key missing VI: {key}" for key in missing_used_vi)
errors.extend(f"used key missing JA: {key}" for key in missing_used_ja)
errors.extend(f"placeholder mismatch: {item}" for item in placeholder_mismatch)

if raw_candidates:
    print("\nPotential hard-coded UI literals:")
    for item in raw_candidates:
        print(item)

if errors:
    print("\nLocalization validation errors:")
    for item in errors:
        print(item)
    raise SystemExit(1)

print("Localization catalog validation: PASS")
