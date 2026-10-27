#!/usr/bin/env python3
"""Build Linux x64 .NET Lambda ZIPs locally; never calls AWS or Terraform."""
import os
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PROJECTS = {
    "api-handler": "APIHandler",
    "reports-handler": "ReportsHandler",
    "report-sender": "ReportSender",
}


def main():
    dotnet = shutil.which("dotnet")
    if not dotnet and Path("/usr/local/share/dotnet/dotnet").is_file():
        dotnet = "/usr/local/share/dotnet/dotnet"
    if not dotnet:
        raise SystemExit("Install the .NET 10 SDK and add dotnet to PATH first.")

    env = os.environ.copy()
    # Keep all local tool caches out of source control and inside this workspace.
    env.update({
        "DOTNET_CLI_HOME": str(ROOT / ".build/dotnet"),
        "NUGET_PACKAGES": str(ROOT / ".build/nuget/packages"),
        "NUGET_HTTP_CACHE_PATH": str(ROOT / ".build/nuget/http-cache"),
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_NOLOGO": "1",
    })
    version = subprocess.check_output([dotnet, "--version"], env=env, text=True).strip()
    if not version.startswith("10."):
        raise SystemExit(f"Use .NET SDK 10 for this project; found {version}.")

    artifacts = ROOT / "artifacts/lambdas"
    artifacts.mkdir(parents=True, exist_ok=True)
    for name, assembly in PROJECTS.items():
        project = ROOT / f"backend/restaurant-backend-app/dnapp/lambdas/{name}/{assembly}.csproj"
        output = ROOT / f".build/publish/{name}"
        archive = artifacts / f"{name}.zip"
        # A failed build must not leave a previous, deployable-looking ZIP behind.
        archive.unlink(missing_ok=True)
        if output.exists():
            shutil.rmtree(output)
        subprocess.run([
            dotnet, "publish", str(project), "--configuration", "Release",
            "--framework", "net10.0", "--runtime", "linux-x64",
            "--self-contained", "false", "-p:PublishReadyToRun=false",
            "--output", str(output), "--nologo",
        ], env=env, check=True)

        for suffix in [".dll", ".deps.json", ".runtimeconfig.json"]:
            if not (output / f"{assembly}{suffix}").is_file():
                raise SystemExit(f"Build did not produce {assembly}{suffix}.")
        files = sorted(p for p in output.rglob("*") if p.is_file())
        if sum(p.stat().st_size for p in files) > 250 * 1024 * 1024:
            raise SystemExit(f"{name} exceeds Lambda's 250 MiB uncompressed ZIP limit.")

        # Stable metadata avoids code updates caused only by ZIP file timestamps.
        with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as bundle:
            for path in files:
                entry = zipfile.ZipInfo(path.relative_to(output).as_posix(), (1980, 1, 1, 0, 0, 0))
                entry.compress_type = zipfile.ZIP_DEFLATED
                entry.external_attr = (0o100755 if os.access(path, os.X_OK) else 0o100644) << 16
                bundle.writestr(entry, path.read_bytes())
        print(f"Built {archive.relative_to(ROOT)} ({archive.stat().st_size / 1024 / 1024:.1f} MiB)")


if __name__ == "__main__":
    try:
        main()
    except subprocess.CalledProcessError as error:
        raise SystemExit(f"Build failed (exit {error.returncode}); fix the error above and run again.") from None
