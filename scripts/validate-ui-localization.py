from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SHARED = ROOT / "FVN_REGISTER.Shared"
LOCALIZATION = SHARED / "Localization"

PAIR_RE = re.compile(r'\["(?P<key>[^"]+)"\]\s*=\s*"(?P<value>(?:\\.|[^"\\])*)"')
LANGUAGE_KEY_RE = re.compile(r'Language\.T\(\s*"([^"]+)"')
STORE_KEY_RE = re.compile(r'LocalizationStore\.Get\([^,]+,\s*"([^"]+)"')
ATTR_RE = re.compile(r'\b(?:Label|Text|Title|Placeholder|HelperText|ToolTip|Tooltip|AriaLabel|Description)="(?P<value>[^"\r\n]+)"')
TEXT_RE = re.compile(r'>(?P<text>[^<>@\r\n]+)<')
SNACKBAR_RE = re.compile(r'Snackbar\.Add\(\s*"(?P<value>[^"]+)"')
PLACEHOLDER_RE = re.compile(r"\{(\d+)\}")

def looks_like_ui_text(value: str) -> bool:
    if not value or value.startswith("@") or "://" in value:
        return False
    if re.fullmatch(r"[A-Za-z0-9_./:%+\-–—→•()\[\]{}]+", value):
        return False
    return bool(re.search(r"[A-Za-zÀ-ỹぁ-んァ-ヶ一-龯]", value))


parser = argparse.ArgumentParser()
parser.add_argument("--output", default="", help="Optional JSON audit output path.")
args = parser.parse_args()

catalogs: dict[str, dict[str, str]] = {"vi": {}, "ja": {}}
duplicates: list[str] = []
modules: dict[str, set[str]] = {"vi": set(), "ja": set()}

# JSON is the runtime source of truth: Localization/{lang}.{module}.json.
for path in sorted(LOCALIZATION.glob("vi.*.json")):
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise SystemExit(f"{path.relative_to(ROOT)} must contain a JSON object")
    module_name = path.name.split(".", 2)[1]
    modules["vi"].add(module_name)
    for key, value in data.items():
        if not isinstance(value, str):
            raise SystemExit(f"{path.relative_to(ROOT)}: '{key}' must have a string value")
        if key in catalogs["vi"]:
            duplicates.append(f"{path.relative_to(ROOT)}: duplicate VI key {key}")
        catalogs["vi"][key] = value

for path in sorted(LOCALIZATION.glob("ja.*.json")):
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise SystemExit(f"{path.relative_to(ROOT)} must contain a JSON object")
    module_name = path.name.split(".", 2)[1]
    modules["ja"].add(module_name)
    for key, value in data.items():
        if not isinstance(value, str):
            raise SystemExit(f"{path.relative_to(ROOT)}: '{key}' must have a string value")
        if key in catalogs["ja"]:
            duplicates.append(f"{path.relative_to(ROOT)}: duplicate JA key {key}")
        catalogs["ja"][key] = value

all_keys = {"vi": set(catalogs["vi"]), "ja": set(catalogs["ja"])}
module_mismatch = sorted(modules["vi"] ^ modules["ja"])
missing_ja = sorted(all_keys["vi"] - all_keys["ja"])
missing_vi = sorted(all_keys["ja"] - all_keys["vi"])

used: set[str] = set()
raw_candidates: list[dict[str, object]] = []

for path in sorted(SHARED.rglob("*.razor")):
    if any(part in {"bin", "obj"} for part in path.parts):
        continue

    text = path.read_text(encoding="utf-8-sig")
    used.update(LANGUAGE_KEY_RE.findall(text))
    used.update(STORE_KEY_RE.findall(text))

    for line_no, line in enumerate(text.splitlines(), 1):
        if "Language.T(" in line:
            # Keys are handled above; only inspect literal UI attributes/text here.
            pass

        for match in ATTR_RE.finditer(line):
            value = match.group("value").strip()
            if looks_like_ui_text(value):
                raw_candidates.append({
                    "file": str(path.relative_to(ROOT)),
                    "line": line_no,
                    "kind": "attribute",
                    "text": value,
                })

        for match in TEXT_RE.finditer(line):
            value = match.group("text").strip()
            if looks_like_ui_text(value):
                raw_candidates.append({
                    "file": str(path.relative_to(ROOT)),
                    "line": line_no,
                    "kind": "text",
                    "text": value,
                })

        for match in SNACKBAR_RE.finditer(line):
            value = match.group("value").strip()
            if looks_like_ui_text(value):
                raw_candidates.append({
                    "file": str(path.relative_to(ROOT)),
                    "line": line_no,
                    "kind": "snackbar",
                    "text": value,
                })

for path in sorted(SHARED.rglob("*.cs")):
    if any(part in {"bin", "obj"} for part in path.parts):
        continue
    text = path.read_text(encoding="utf-8-sig")
    used.update(LANGUAGE_KEY_RE.findall(text))
    used.update(STORE_KEY_RE.findall(text))

missing_used_vi = sorted(k for k in used if k not in all_keys["vi"])
missing_used_ja = sorted(k for k in used if k not in all_keys["ja"])

placeholder_mismatch = []
for key in sorted(all_keys["vi"] & all_keys["ja"]):
    vi = sorted(PLACEHOLDER_RE.findall(catalogs["vi"][key]))
    ja = sorted(PLACEHOLDER_RE.findall(catalogs["ja"][key]))
    if vi != ja:
        placeholder_mismatch.append({"key": key, "vi": vi, "ja": ja})

unused = sorted(k for k in all_keys["vi"] | all_keys["ja"] if k not in used)

errors: list[str] = []
errors.extend(duplicates)
errors.extend(f"module file missing language pair: {module}" for module in module_mismatch)
errors.extend(f"missing JA: {key}" for key in missing_ja)
errors.extend(f"missing VI: {key}" for key in missing_vi)
errors.extend(f"used key missing VI: {key}" for key in missing_used_vi)
errors.extend(f"used key missing JA: {key}" for key in missing_used_ja)
errors.extend(f"placeholder mismatch: {item['key']} VI={item['vi']} JA={item['ja']}" for item in placeholder_mismatch)

audit = {
    "catalogKeys": {"vi": len(all_keys["vi"]), "ja": len(all_keys["ja"])},
    "catalogModules": {"vi": sorted(modules["vi"]), "ja": sorted(modules["ja"])},
    "moduleMismatches": module_mismatch,
    "usedKeys": len(used),
    "missingVi": missing_vi,
    "missingJa": missing_ja,
    "missingUsedVi": missing_used_vi,
    "missingUsedJa": missing_used_ja,
    "unusedKeys": unused,
    "placeholderMismatch": placeholder_mismatch,
    "hardCodedUi": raw_candidates,
    "errors": errors,
}

print(f"Catalog keys: VI={len(all_keys['vi'])}, JA={len(all_keys['ja'])}")
print(f"Used localization keys: {len(used)}")
print(f"Catalog modules: VI={len(modules["vi"])}, JA={len(modules["ja"])}")
print(f"Potential hard-coded UI candidates: {len(raw_candidates)}")
print(f"Unused keys: {len(unused)}")

if raw_candidates:
    print("\nPotential hard-coded UI literals (audit only):")
    for item in raw_candidates:
        print(f"{item['file']}:{item['line']}: {item['kind']}: {item['text']}")

if errors:
    print("\nLocalization validation errors:")
    for item in errors:
        print(item)

if args.output:
    output = Path(args.output)
    if not output.is_absolute():
        output = ROOT / output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(audit, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Audit JSON: {output}")

if errors:
    raise SystemExit(1)

print("Localization catalog validation: PASS")


def looks_like_ui_text(value: str) -> bool:
    if not value or value.startswith("@") or "://" in value:
        return False
    if re.fullmatch(r"[A-Za-z0-9_./:%+\-–—→•()\[\]{}]+", value):
        return False
    return bool(re.search(r"[A-Za-zÀ-ỹぁ-んァ-ヶ一-龯]", value))
