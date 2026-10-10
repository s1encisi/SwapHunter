#!/usr/bin/env python3
"""Independently verify and extract a SwapHunter v0.6.0 delivery ZIP.

This checks package integrity, including every ZIP member's CRC, size and SHA256,
then checks a fresh extraction. It does not reproduce a Unity build or prove
runtime correctness. No original source/build directory is read for verification.
The extraction directory must not exist; partial extraction is kept on failure.
Only Python's standard library is used; no external program is started.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys
import unicodedata
import zipfile


MANIFEST_NAME = "package-manifest.json"
CHUNK_SIZE = 1024 * 1024
_SHA256 = re.compile(r"[0-9a-fA-F]{64}\Z")
_RESERVED = re.compile(r"(?:CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\..*)?\Z", re.I)


class PackageVerificationError(ValueError):
    """The archive, manifest, or independent extraction failed verification."""


def _fail(message: str) -> None:
    raise PackageVerificationError(message)


def _key(path: str) -> str:
    return unicodedata.normalize("NFC", path).casefold()


def _parts(path: str, *, directory: bool = False) -> tuple[str, ...]:
    if not isinstance(path, str) or not path:
        _fail("A package path must be a nonempty string")
    if "\\" in path or path.startswith("/"):
        _fail(f"Unsafe package path: {path!r}")
    value = path[:-1] if directory and path.endswith("/") else path
    parts = tuple(value.split("/"))
    for part in parts:
        if (not part or part in (".", "..") or part.endswith((".", " "))
                or any(ord(c) < 32 or c in '<>:"|?*' for c in part)
                or _RESERVED.fullmatch(part)):
            _fail(f"Unsafe or Windows-incompatible package path: {path!r}")
    return parts


def _json_bytes(data: bytes, label: str) -> dict:
    def unique_object(pairs: list[tuple[str, object]]) -> dict:
        value = {}
        for name, item in pairs:
            if name in value:
                _fail(f"Duplicate JSON property {name!r} in {label}")
            value[name] = item
        return value

    def invalid_constant(value: str) -> None:
        _fail(f"Invalid JSON constant {value!r} in {label}")

    try:
        parsed = json.loads(data.decode("utf-8-sig"), object_pairs_hook=unique_object,
                            parse_constant=invalid_constant)
    except (UnicodeError, json.JSONDecodeError) as error:
        raise PackageVerificationError(f"Invalid JSON in {label}: {error}") from error
    if not isinstance(parsed, dict):
        _fail(f"{label} must contain a JSON object")
    return parsed


def _manifest(data: bytes) -> tuple[dict, dict[str, dict], set[str]]:
    manifest = _json_bytes(data, MANIFEST_NAME)
    if type(manifest.get("schemaVersion")) is not int or manifest["schemaVersion"] != 1:
        _fail("Manifest schemaVersion must be integer 1")
    if manifest.get("artifact") not in ("windows", "source"):
        _fail("Manifest artifact must be 'windows' or 'source'")
    if manifest.get("version") != "0.6.0":
        _fail("Manifest version must be '0.6.0'")
    root = manifest.get("archiveRoot")
    if len(_parts(root)) != 1:
        _fail("Manifest archiveRoot must be one directory name")
    items = manifest.get("files")
    if not isinstance(items, list):
        _fail("Manifest files must be a list")

    # Include implied directories in the namespace so a file cannot also be a
    # directory, and different capitalization of parent folders cannot collide.
    namespace: dict[str, tuple[str, bool]] = {}

    def register(path: str, directory: bool) -> None:
        identity = _key(path)
        previous = namespace.get(identity)
        if previous is not None and previous != (path, directory):
            _fail(f"Manifest path collision: {previous[0]!r} and {path!r}")
        namespace[identity] = (path, directory)

    files: dict[str, dict] = {}
    directories = {root}
    register(root, True)
    register(f"{root}/{MANIFEST_NAME}", False)
    for item in items:
        if not isinstance(item, dict):
            _fail("Each manifest files entry must be an object")
        path = item.get("path")
        parts = _parts(path)
        if _key(path) == _key(MANIFEST_NAME):
            _fail("The manifest must not list itself")
        length = item.get("bytes")
        digest = item.get("sha256")
        if type(length) is not int or length < 0:
            _fail(f"Invalid byte count for {path!r}")
        if not isinstance(digest, str) or not _SHA256.fullmatch(digest):
            _fail(f"Invalid SHA256 for {path!r}")
        full_path = f"{root}/{path}"
        if full_path in files:
            _fail(f"Duplicate manifest file: {path!r}")
        register(full_path, False)
        for index in range(1, len(parts)):
            parent = "/".join((root, *parts[:index]))
            register(parent, True)
            directories.add(parent)
        files[full_path] = {"bytes": length, "sha256": digest.lower()}
    return manifest, files, directories


def _digest_stream(stream) -> tuple[int, str]:
    digest = hashlib.sha256()
    length = 0
    while True:
        chunk = stream.read(CHUNK_SIZE)
        if not chunk:
            break
        digest.update(chunk)
        length += len(chunk)
    return length, digest.hexdigest()


def _digest_file(path: Path) -> tuple[int, str]:
    with path.open("rb") as stream:
        return _digest_stream(stream)


def _compare(path: str, actual: tuple[int, str], expected: dict, phase: str) -> None:
    if actual != (expected["bytes"], expected["sha256"]):
        _fail(f"{phase} size/SHA256 mismatch for {path!r}")


def _verify_archive(archive: Path, destination: Path,
                    external_manifest: Path | None) -> dict:
    if os.path.lexists(destination):
        _fail(f"Extraction destination already exists: {destination}")
    if not destination.parent.is_dir():
        _fail(f"Extraction destination parent does not exist: {destination.parent}")
    with archive.open("rb") as archive_stream:
        _, archive_sha = _digest_stream(archive_stream)
        archive_stream.seek(0)
        with zipfile.ZipFile(archive_stream, "r") as package:
            entries: dict[str, zipfile.ZipInfo] = {}
            seen: dict[str, str] = {}
            manifest_names = []
            for info in package.infolist():
                # ZipInfo truncates filename at NUL but retains orig_filename.
                if info.orig_filename != info.filename:
                    _fail(f"ZIP filename was truncated: {info.orig_filename!r}")
                parts = _parts(info.filename, directory=info.is_dir())
                name = "/".join(parts)
                identity = _key(name)
                if identity in seen:
                    _fail(f"Duplicate/case-colliding ZIP entries: {seen[identity]!r}, {info.filename!r}")
                seen[identity] = info.filename
                mode = stat.S_IFMT(info.external_attr >> 16)
                if mode == stat.S_IFLNK:
                    _fail(f"Symbolic link ZIP entry: {info.filename!r}")
                if mode not in (0, stat.S_IFREG, stat.S_IFDIR):
                    _fail(f"Unsupported special ZIP entry: {info.filename!r}")
                if (mode == stat.S_IFDIR and not info.is_dir()) or (mode == stat.S_IFREG and info.is_dir()):
                    _fail(f"ZIP directory type mismatch: {info.filename!r}")
                if info.flag_bits & 1:
                    _fail(f"Encrypted ZIP entry: {info.filename!r}")
                entries[info.filename] = info
                if not info.is_dir() and len(parts) == 2 and parts[1] == MANIFEST_NAME:
                    manifest_names.append(info.filename)
            if len(manifest_names) != 1:
                _fail("ZIP must contain exactly one root/package-manifest.json")
            manifest_name = manifest_names[0]
            manifest_bytes = package.read(entries[manifest_name])
            manifest, expected, directories = _manifest(manifest_bytes)
            root = manifest["archiveRoot"]
            if manifest_name != f"{root}/{MANIFEST_NAME}":
                _fail("Manifest archiveRoot does not match its ZIP location")
            if external_manifest is not None:
                outside = external_manifest.read_bytes()
                if outside != manifest_bytes:
                    outside_manifest, _, _ = _manifest(outside)
                    # Python considers True == 1 and 1.0 == 1. Canonical JSON
                    # preserves their types while ignoring object-key order and
                    # whitespace, so a changed JSON value cannot compare equal.
                    canonical = lambda value: json.dumps(value, sort_keys=True, ensure_ascii=False,
                                                         separators=(",", ":"))
                    if canonical(outside_manifest) != canonical(manifest):
                        _fail("External manifest differs from the manifest inside the ZIP")
            manifest_sha = hashlib.sha256(manifest_bytes).hexdigest()
            expected[manifest_name] = {"bytes": len(manifest_bytes), "sha256": manifest_sha}
            actual_files = {name for name, info in entries.items() if not info.is_dir()}
            if actual_files != set(expected):
                missing = sorted(set(expected) - actual_files)
                extra = sorted(actual_files - set(expected))
                _fail(f"ZIP file inventory differs from manifest: missing={missing!r}, extra={extra!r}")
            for name, info in entries.items():
                if info.is_dir():
                    if name[:-1] not in directories:
                        _fail(f"Unexpected directory ZIP entry: {name!r}")
                    # Reading also validates the CRC of directory records.
                    if package.read(info) != b"":
                        _fail(f"Directory ZIP entry contains data: {name!r}")
                else:
                    if info.file_size != expected[name]["bytes"]:
                        _fail(f"ZIP header size differs from manifest: {name!r}")
                    with package.open(info, "r") as stream:
                        _compare(name, _digest_stream(stream), expected[name], "ZIP readback")

            # Only after complete archive validation do we create an independent
            # destination. Never extractall: write only the already audited names.
            destination.mkdir()
            for name in sorted(expected):
                output = destination.joinpath(*name.split("/"))
                output.parent.mkdir(parents=True, exist_ok=True)
                with package.open(entries[name], "r") as source, output.open("xb") as target:
                    while True:
                        chunk = source.read(CHUNK_SIZE)
                        if not chunk:
                            break
                        target.write(chunk)

        # Rehash the same archive handle to reject an archive changed mid-run.
        archive_stream.seek(0)
        if _digest_stream(archive_stream)[1] != archive_sha:
            _fail("Archive changed while it was being verified")

    extracted_files = set()
    extracted_directories = set()
    for current, dirnames, filenames in os.walk(destination, followlinks=False):
        for name in dirnames + filenames:
            path = Path(current) / name
            attributes = path.lstat()
            if stat.S_ISLNK(attributes.st_mode) or getattr(attributes, "st_file_attributes", 0) & 0x400:
                _fail(f"Link/reparse point found in independent extraction: {path}")
            relative = path.relative_to(destination).as_posix()
            if stat.S_ISDIR(attributes.st_mode):
                extracted_directories.add(relative)
            elif stat.S_ISREG(attributes.st_mode):
                extracted_files.add(relative)
            else:
                _fail(f"Special file found in independent extraction: {path}")
    if extracted_files != set(expected) or extracted_directories != directories:
        _fail("Independent extraction inventory differs from the manifest")
    for name, identity in expected.items():
        _compare(name, _digest_file(destination.joinpath(*name.split("/"))), identity, "Independent extraction")
    return {
        "passed": True,
        "fileCount": len(manifest["files"]),
        "archiveSha256": archive_sha,
        "manifestSha256": manifest_sha,
        "artifactRoot": str((destination / root).resolve()),
    }


def verify_archive(archive: Path, destination: Path,
                   external_manifest: Path | None = None) -> dict:
    """Verify ZIP and a new extraction; raise PackageVerificationError on failure.

    ``destination.parent`` must already exist. The destination itself must not
    exist, even as an empty directory or broken symlink. Files created there are
    retained on failure. ``external_manifest`` may differ only in JSON formatting.
    ``fileCount`` excludes package-manifest.json. This is integrity verification,
    not Unity build reproduction or runtime QA.
    """
    try:
        return _verify_archive(Path(archive), Path(destination),
                               Path(external_manifest) if external_manifest is not None else None)
    except PackageVerificationError:
        raise
    # ZIP decompressor exceptions vary with the stored compression method
    # (zlib.error, LZMAError, OSError, etc.). Normalize all ordinary failures at
    # this public boundary so the CLI can always write a truthful failed report.
    except Exception as error:
        raise PackageVerificationError(f"Package verification failed: {error}") from error


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", required=True, type=Path)
    parser.add_argument("--destination", required=True, type=Path)
    parser.add_argument("--manifest", type=Path, help="Optional external copy of package-manifest.json")
    parser.add_argument("--report", type=Path, help="New JSON report path; never overwrites an existing file")
    args = parser.parse_args(argv)
    report_stream = None
    if args.report is not None:
        try:
            report_stream = args.report.open("x", encoding="utf-8", newline="\n")
        except OSError as error:
            print(f"Cannot create new report: {error}", file=sys.stderr)
            return 1
    try:
        try:
            result = verify_archive(args.archive, args.destination, args.manifest)
        except PackageVerificationError as error:
            result = {"passed": False, "error": str(error),
                      "archive": str(args.archive), "destination": str(args.destination)}
        encoded = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
        if report_stream is not None:
            report_stream.write(encoded)
        print(encoded, end="")
        return 0 if result["passed"] else 1
    finally:
        if report_stream is not None:
            report_stream.close()


if __name__ == "__main__":
    raise SystemExit(main())
