#!/usr/bin/env python3
"""Build an allowlisted, secret-free CodeBuild source ZIP in ignored .build/."""

from pathlib import Path
import json
import zipfile


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "aqa" / "restaurant-aqa-framework"
OUTPUT = ROOT / ".build" / "aqa-source.zip"
SOURCE_SUFFIXES = {".cs", ".csproj", ".slnx", ".feature", ".json"}
EXCLUDED_PARTS = {"bin", "obj", "TestResults", ".playwright-browsers", "node_modules"}
REQUIRED = [
    ROOT / "aqa" / "buildspec.yml",
    *(ROOT / "scripts" / f"run-aqa-level{level}.py" for level in (1, 2, 3)),
    PROJECT / "restaurant-aqa-framework.csproj",
    PROJECT / "ReportPortal.json",
    PROJECT / ".editorconfig",
]


def main() -> int:
    for path in REQUIRED:
        if not path.is_file() or path.is_symlink():
            raise SystemExit(f"Required safe source file is missing: {path.relative_to(ROOT)}")

    reportportal = json.loads((PROJECT / "ReportPortal.json").read_text(encoding="utf-8"))
    if reportportal.get("enabled") or reportportal.get("server", {}).get("apiKey"):
        raise SystemExit("ReportPortal.json must be disabled and contain no API key.")

    files = set(REQUIRED)
    for path in PROJECT.rglob("*"):
        if (path.is_file() and not path.is_symlink()
                and path.suffix in SOURCE_SUFFIXES
                and not path.name.endswith(".feature.cs")):
            if not EXCLUDED_PARTS.intersection(path.relative_to(PROJECT).parts):
                files.add(path)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(OUTPUT, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(files):
            archive.write(path, path.relative_to(ROOT))

    with zipfile.ZipFile(OUTPUT) as archive:
        names = archive.namelist()
        if any(".env" in Path(name).name or "TestResults" in Path(name).parts for name in names):
            raise SystemExit("Source ZIP contains a forbidden environment or result file.")
        if archive.testzip() is not None:
            raise SystemExit("Source ZIP failed integrity check.")
    print(f"Built {OUTPUT.relative_to(ROOT)} with {len(names)} allowlisted source files.")
    print("No .env, test results, compiled output, or browser cache was included.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
