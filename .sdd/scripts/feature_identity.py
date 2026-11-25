"""Suggest numbered SDD IDs or resolve existing IDs without changing files/Git."""
import argparse
from pathlib import Path
import re
import subprocess
import sys


FEATURE_PATTERN = r"(?:(?:00[1-9]|0[1-9][0-9]|[1-9][0-9]{2,})_)?[a-z0-9]+(?:-[a-z0-9]+)*"


def normalize(value):
    value = value.strip().lower()
    if value.endswith(".md"):
        value = value[:-3]
    prefix = re.match(r"^(\d+)_", value)
    number = prefix.group(0) if prefix else ""
    name = value[len(number):]
    name = re.sub(r"[ _]+", "-", name)
    result = number + name
    if not re.fullmatch(FEATURE_PATTERN, result):
        raise ValueError("Use NNN_feature-name or a legacy feature-name; no paths or zero ID")
    return result


def suffix(value):
    return re.sub(r"^\d+_", "", value)


def inventory(root):
    folder = root / ".specs"
    names = {p.name for p in folder.iterdir() if p.is_dir()} if folder.is_dir() else set()
    refs = subprocess.run(["git", "-C", str(root), "for-each-ref", "--format=%(refname)",
                           "refs/heads", "refs/remotes"], capture_output=True, text=True, check=True)
    for ref in refs.stdout.splitlines():
        match = re.fullmatch(r"refs/(?:heads|remotes/[^/]+)/feature/(.+)", ref)
        if match:
            names.add(match.group(1))
    return {name for name in names if re.fullmatch(FEATURE_PATTERN, name)}


def check_numbers(names):
    numbers = {}
    for name in sorted(names):
        match = re.match(r"^(\d+)_", name)
        if match:
            number = int(match.group(1))
            if number in numbers:
                raise ValueError(f"Number collision: {numbers[number]}, {name}")
            numbers[number] = name
    return numbers


def next_id(value, names):
    normalized = normalize(value)
    numbers = check_numbers(names)
    existing = sorted(name for name in names if suffix(name) == suffix(normalized))
    if existing:
        raise ValueError("Feature already exists; resolve instead: " + ", ".join(existing))
    number = max(numbers, default=0) + 1
    result = f"{number:03d}_{suffix(normalized)}"
    if re.match(r"^\d+_", normalized) and normalized != result:
        raise ValueError(f"Next ID is {result}; supplied number is not next")
    return result


def resolve(value, names):
    normalized = normalize(value)
    if normalized in names:
        matches = [normalized]
    elif re.match(r"^\d+_", normalized):
        matches = []
    else:
        matches = sorted(name for name in names if suffix(name) == normalized)
    if not matches:
        raise ValueError(f"Unknown feature: {normalized}; run $theshop-start first")
    if len(matches) > 1:
        raise ValueError("Ambiguous feature; use full ID: " + ", ".join(matches))
    chosen = matches[0]
    prefix = re.match(r"^\d+_", chosen)
    if prefix:
        check_numbers({name for name in names if name.startswith(prefix.group(0))})
    return chosen


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("next", "resolve"))
    parser.add_argument("feature")
    args = parser.parse_args()
    try:
        names = inventory(Path(__file__).resolve().parents[2])
        print((next_id if args.mode == "next" else resolve)(args.feature, names))
        return 0
    except (ValueError, OSError, subprocess.CalledProcessError) as exc:
        print(f"[feature-identity] {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
