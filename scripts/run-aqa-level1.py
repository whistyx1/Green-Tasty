#!/usr/bin/env python3
"""Preflight, discover, or explicitly run only the level-one AQA scenarios."""

import argparse
import datetime
import io
import html
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import uuid
from urllib.parse import quote
import xml.etree.ElementTree as ET


REPOSITORY = Path(__file__).resolve().parents[1]
PROJECT = REPOSITORY / "aqa" / "restaurant-aqa-framework"
ASSEMBLY = PROJECT / "bin" / "Release" / "net10.0" / "restaurant-aqa-framework.dll"
REQUIRED = (
    "UI_URL", "API_URL", "OWNER_USERNAME", "OWNER_PASSWORD",
    "OTHER_CUSTOMER_USERNAME", "OTHER_CUSTOMER_PASSWORD",
    "WAITER_USERNAME", "WAITER_PASSWORD", "ADMIN_USERNAME", "ADMIN_PASSWORD",
)
TAGS = {"all": "level1", "api": "level1api", "ui": "level1ui"}


def configured_values():
    """Read presence for preflight only; dotenv.net remains the runtime parser."""
    values = {}
    dotenv = PROJECT / ".env"
    if dotenv.is_file():
        for line in dotenv.read_text(encoding="utf-8-sig").splitlines():
            name, separator, value = line.strip().partition("=")
            if separator and not name.startswith("#"):
                values[name.strip()] = value.strip().strip("\"'")
    values.update(os.environ)
    return values


def redact(text, values):
    reportportal_key = values.get("REPORTPORTAL_API_KEY", "")
    if reportportal_key:
        text = text.replace(reportportal_key, "[private ReportPortal key]")
    for name in REQUIRED:
        value = values.get(name, "")
        if name.endswith(("PASSWORD", "USERNAME")) and value:
            for representation in {value, html.escape(value, quote=True), json.dumps(value)[1:-1], quote(value, safe="")}:
                text = text.replace(representation, "[private account value]")
    text = re.sub(r"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[token]", text)
    text = re.sub(r"(?i)Bearer\s+[A-Za-z0-9+/_=-]+", "Bearer [token]", text)
    return re.sub(r"/join/[A-Za-z0-9_-]+", "/join/[private invite]", text)


def sanitize_artifacts(directory, values):
    for path in directory.rglob("*"):
        if path.is_file() and path.suffix.lower() in {".trx", ".log", ".json", ".ndjson", ".html"}:
            content = path.read_text(encoding="utf-8")
            path.write_text(redact(content, values), encoding="utf-8")


def execute(command, environment, values, log=None):
    process = subprocess.Popen(
        command, cwd=PROJECT, env=environment, stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, text=True,
    )
    for line in process.stdout:
        safe = redact(line, values)
        print(safe, end="", flush=True)
        if log:
            log.write(safe)
    return process.wait()


def validate_reports(directory, expected):
    paths = list(directory.rglob("*.trx"))
    if not paths:
        print("No current TRX report: this run is not verified.")
        return False
    total = passed = failed = skipped = 0
    for path in paths:
        root = ET.parse(path).getroot()
        namespace = {"t": root.tag.split("}")[0].strip("{")}
        counters = root.find(".//t:ResultSummary/t:Counters", namespace)
        if counters is None:
            print("Invalid TRX report: counters missing.")
            return False
        total += int(counters.get("total", "0"))
        passed += int(counters.get("passed", "0"))
        failed += int(counters.get("failed", "0"))
        skipped += int(counters.get("notExecuted", "0"))
    print(f"Verified TRX: total={total}, passed={passed}, failed={failed}, not executed={skipped}.")
    if total != expected:
        print(f"Expected {expected} discovered cases; current report contains {total}.")
        return False
    return total > 0 and passed == total and failed == 0 and skipped == 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="Check configuration names only (default).")
    mode.add_argument("--list", action="store_true", help="Discover level-one cases without executing them.")
    mode.add_argument("--live", action="store_true", help="Explicitly run against the configured deployment.")
    parser.add_argument("--scope", choices=TAGS, default="all")
    parser.add_argument("--no-build", action="store_true", help="Use an already-built Release assembly.")
    args = parser.parse_args()
    values = configured_values()
    if not args.list:
        missing = [name for name in REQUIRED if not values.get(name)]
        if missing:
            print("Configure the ignored AQA .env or environment; missing names:")
            print("\n".join(f"- {name}" for name in missing))
            print("Do not paste credentials into chat or commit them.")
            return 2
        if values["OWNER_USERNAME"].casefold() == values["OTHER_CUSTOMER_USERNAME"].casefold():
            print("OWNER_USERNAME and OTHER_CUSTOMER_USERNAME must be different Customers.")
            return 2
        if not args.live:
            print("Required configuration names are populated. No authentication or live test was attempted.")
            return 0
    dotnet = shutil.which("dotnet")
    if not dotnet and Path("/usr/local/share/dotnet/dotnet").is_file():
        dotnet = "/usr/local/share/dotnet/dotnet"
    if not dotnet:
        print("Install/configure the .NET 10 SDK before building AQA.")
        return 2
    # Pass validated local values to the child process explicitly. The test
    # framework's dotenv loader may not discover a file when probing is off;
    # values stay in the process environment, never in command arguments.
    environment = os.environ.copy()
    environment.update({name: values[name] for name in REQUIRED if values.get(name)})
    environment.update({
        "SKIP_PRE_RUN_CLEANUP": "true", "MAX_PARALLEL_TESTS": "1",
        "PLAYWRIGHT_BROWSERS_PATH": str(PROJECT / ".playwright-browsers"),
        "TRACE_MODE": "Never", "TESTINGPLATFORM_TELEMETRY_OPTOUT": "1",
    })
    reportportal_enabled = values.get("REPORTPORTAL_ENABLED", "").lower() == "true"
    if reportportal_enabled and args.live:
        reportportal_names = ("REPORTPORTAL_URL", "REPORTPORTAL_PROJECT", "REPORTPORTAL_API_KEY")
        missing_reportportal = [name for name in reportportal_names if not values.get(name)]
        if missing_reportportal:
            print("ReportPortal reporting requested, but these private settings are missing:")
            print("\n".join(f"- {name}" for name in missing_reportportal))
            return 2
        environment.update({
            "ReportPortal__Enabled": "true",
            "ReportPortal__Server__Url": values["REPORTPORTAL_URL"],
            "ReportPortal__Server__Project": values["REPORTPORTAL_PROJECT"],
            "ReportPortal__Server__ApiKey": values["REPORTPORTAL_API_KEY"],
        })
    else:
        environment["ReportPortal__Enabled"] = "false"
    if not args.no_build:
        code = execute([dotnet, "build", "restaurant-aqa-framework.csproj", "--configuration", "Release"], environment, values)
        if code:
            return code
    if not ASSEMBLY.is_file():
        print("Release assembly is missing; rerun without --no-build.")
        return 2
    command = [dotnet, str(ASSEMBLY), "--treenode-filter", f"/*/*/*/*[Category={TAGS[args.scope]}]"]
    listing = io.StringIO()
    discovery_environment = {**environment, "UI_URL": "", "API_URL": "", "ReportPortal__Enabled": "false"}
    code = execute(command + ["--list-tests"], discovery_environment, values, listing)
    discovered = re.search(r"found (\d+) test\(s\)", listing.getvalue())
    if code or not discovered or int(discovered.group(1)) == 0:
        print("Nonzero level-one discovery was not verified; no live test was started.")
        return code or 1
    expected = int(discovered.group(1))
    if args.list:
        return 0
    timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    output = PROJECT / "TestResults" / f"level1-{timestamp}-{uuid.uuid4().hex[:8]}"
    output.mkdir(parents=True)
    environment["ARTIFACTS_DIR"] = str(output)
    started = time.time()
    with (output / "run.log").open("w", encoding="utf-8") as log:
        code = execute(command + ["--report-trx", "--results-directory", str(output)], environment, values, log)
    # Reqnroll's existing formatter has a fixed relative output directory.
    reports = PROJECT / "TestResults" / "reqnroll"
    if reports.is_dir() and any(p.stat().st_mtime >= started for p in reports.rglob("*") if p.is_file()):
        shutil.copytree(reports, output / "reqnroll", dirs_exist_ok=True)
    sanitize_artifacts(output, values)
    print(f"Local reports: {output.relative_to(REPOSITORY)}")
    try:
        verified = validate_reports(output, expected)
    except (ET.ParseError, OSError, ValueError):
        print("Current reports could not be parsed; no passing result is claimed.")
        verified = False
    (output / "summary.json").write_text(json.dumps({
        "scope": args.scope, "discovered": expected, "process_exit_code": code,
        "verified_all_passed": verified and code == 0,
    }, indent=2) + "\n", encoding="utf-8")
    return code if code else (0 if verified else 1)


if __name__ == "__main__":
    sys.exit(main())
