#!/usr/bin/env python3
"""Build the ARM64 Lambda artifact for MuscleCuties.Server.

The script publishes the server for linux-arm64, removes the previous
artifacts/MuscleCuties.Server.zip, and writes a clean zip plus its SHA-256
sidecar and build-manifest.json. It never prints connection strings or
credentials; no AWS credentials are needed because upload stays manual.

Usage:
    python3 tools/package_server.py
    python3 tools/package_server.py --skip-publish --publish-dir /tmp/publish
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "services" / "MuscleCuties.Server" / "MuscleCuties.Server.csproj"
ARTIFACT_DIR = ROOT / "artifacts"
ZIP_NAME = "MuscleCuties.Server.zip"
RUNTIME = "linux-arm64"
LAMBDA_RUNTIME = "dotnet10"
ARCHITECTURE = "arm64"

# Build/debug by-products and known strays that must never be uploaded.
EXCLUDED_NAMES = {
    ".DS_Store",
    "appsettings.Development.json",
    "SnapshotRestore.Registry.dll",
    "MuscleCuties.Server.staticwebassets.endpoints.json",
}
EXCLUDED_SUFFIXES = (".pdb", ".zip", ".sha256")
EXCLUDED_DIRECTORIES = {"__MACOSX"}

REQUIRED_FILES = {
    "MuscleCuties.Server",
    "MuscleCuties.Server.dll",
    "MuscleCuties.Server.deps.json",
    "MuscleCuties.Server.runtimeconfig.json",
    "appsettings.json",
    "libSkiaSharp.so",
    "Amazon.Lambda.AspNetCoreServer.Hosting.dll",
}


def run(command: list[str], *, cwd: Path = ROOT) -> None:
    printable = " ".join(command)
    print(f"+ {printable}", flush=True)
    completed = subprocess.run(command, cwd=cwd, check=False)
    if completed.returncode != 0:
        raise SystemExit(f"command failed with exit code {completed.returncode}: {printable}")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git_source_sha() -> str | None:
    try:
        completed = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError):
        return None
    return completed.stdout.strip() or None


def is_excluded(relative: Path) -> bool:
    if any(part in EXCLUDED_DIRECTORIES for part in relative.parts):
        return True
    if relative.name in EXCLUDED_NAMES:
        return True
    return relative.name.endswith(EXCLUDED_SUFFIXES)


def collect_publish_files(publish_dir: Path) -> list[Path]:
    files = sorted(path for path in publish_dir.rglob("*") if path.is_file())
    if not files:
        raise SystemExit(f"publish directory is empty: {publish_dir}")

    present = {path.relative_to(publish_dir).as_posix() for path in files}
    missing = sorted(REQUIRED_FILES - present)
    if missing:
        raise SystemExit("publish output is missing required Lambda files: " + ", ".join(missing))
    return files


def write_archive(publish_dir: Path, destination: Path) -> tuple[list[dict[str, object]], int]:
    entries: list[dict[str, object]] = []
    skipped: list[str] = []
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in collect_publish_files(publish_dir):
            relative = path.relative_to(publish_dir)
            if is_excluded(relative):
                skipped.append(relative.as_posix())
                continue
            archive.write(path, relative.as_posix())
            entries.append(
                {
                    "path": relative.as_posix(),
                    "bytes": path.stat().st_size,
                    "sha256": sha256(path),
                }
            )

    if not any(entry["path"] == "MuscleCuties.Server.dll" for entry in entries):
        destination.unlink(missing_ok=True)
        raise SystemExit("refusing to write an archive without MuscleCuties.Server.dll")

    if skipped:
        print("excluded from the Lambda zip:")
        for name in skipped:
            print(f"  - {name}")
    return entries, destination.stat().st_size


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", default="Release", help="dotnet build configuration")
    parser.add_argument(
        "--skip-publish",
        action="store_true",
        help="zip an existing publish directory instead of running dotnet publish",
    )
    parser.add_argument(
        "--publish-dir",
        type=Path,
        help="existing publish directory (required with --skip-publish)",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=ARTIFACT_DIR,
        help="directory for the zip, checksum, and manifest",
    )
    args = parser.parse_args()

    if args.skip_publish and args.publish_dir is None:
        parser.error("--skip-publish requires --publish-dir")

    output_dir: Path = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    final_zip = output_dir / ZIP_NAME
    checksum_file = output_dir / f"{ZIP_NAME}.sha256"
    manifest_file = output_dir / "build-manifest.json"

    temporary_dir: tempfile.TemporaryDirectory[str] | None = None
    if args.skip_publish:
        publish_dir = args.publish_dir.resolve()
    else:
        temporary_dir = tempfile.TemporaryDirectory(prefix="mc-lambda-")
        publish_dir = Path(temporary_dir.name)
        run(
            [
                "dotnet",
                "publish",
                str(PROJECT),
                "-c",
                args.configuration,
                "-r",
                RUNTIME,
                "--self-contained",
                "false",
                "-o",
                str(publish_dir),
                "-p:DebugType=None",
                "-p:DebugSymbols=false",
                "--nologo",
            ]
        )

    try:
        with tempfile.NamedTemporaryFile(
            prefix=f".{ZIP_NAME}.", suffix=".tmp", dir=output_dir, delete=False
        ) as handle:
            staged_zip = Path(handle.name)

        entries, zip_bytes = write_archive(publish_dir, staged_zip)

        # Only replace the previous artifact after a complete, valid zip exists.
        final_zip.unlink(missing_ok=True)
        checksum_file.unlink(missing_ok=True)
        manifest_file.unlink(missing_ok=True)
        os.replace(staged_zip, final_zip)

        digest = sha256(final_zip)
        checksum_file.write_text(f"{digest}  {ZIP_NAME}\n", encoding="utf-8")

        manifest = {
            "artifact": ZIP_NAME,
            "architecture": ARCHITECTURE,
            "configuration": args.configuration,
            "lambda_runtime": LAMBDA_RUNTIME,
            "runtime_identifier": RUNTIME,
            "generated_at_utc": datetime.now(timezone.utc).isoformat(),
            "source_sha": git_source_sha(),
            "zip_bytes": zip_bytes,
            "zip_sha256": digest,
            "files": entries,
        }
        manifest_file.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    finally:
        if temporary_dir is not None:
            temporary_dir.cleanup()

    print()
    print(f"wrote {final_zip}")
    print(f"  bytes:   {zip_bytes}")
    print(f"  sha256:  {digest}")
    print(f"  sidecar: {checksum_file.name}")
    print(f"  manifest:{manifest_file.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
