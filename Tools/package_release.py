#!/usr/bin/env python3
"""Package an identified SwapHunter Windows release without building or publishing.

The supplied build and current source must match the requested version/commit.
Only the player, public notices and two launchers enter the Windows archive.
Existing output/work directories are never reused. Integrity verification checks
ZIP CRCs, every file's size/SHA-256 and an independent safe extraction; it does
not certify gameplay or replace the project's historical QA gates.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import unicodedata
import zipfile


FORMAT = "swaphunter-windows-release-v1"
MANIFEST = "release-manifest.json"
SHA256 = re.compile(r"[0-9a-f]{64}\Z")
RESERVED = re.compile(r"(?:CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\..*)?\Z", re.I)
PRIVATE_PARTS = {".git", ".codex", ".agents", "logs", "temp", "work", "outputs",
                 "library", "usersettings", "memorycaptures", "recordings", "__pycache__"}
BASE_NOTICES = (
    "LICENSE", "THIRD_PARTY_NOTICES.md", "Game/THIRD_PARTY_NOTICES.md",
    "THIRD_PARTY_ASSETS.md", "THIRD_PARTY_ASSETS.json",
    "Game/Assets/_SwapHunter/Fonts/OFL.txt",
    "Game/Assets/_SwapHunter/Art/ThreeModels/THREE-LICENSE.txt",
    "Tools/VisualAuthoring/Sources/AnimatedCharacters2019/CC0-1.0.txt",
)
NATIVE_FILES = (
    "SwapHunter.exe", "UnityPlayer.dll", "UnityCrashHandler64.exe",
    "D3D12/D3D12Core.dll", "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll",
    "MonoBleedingEdge/EmbedRuntime/MonoPosixHelper.dll",
    "SwapHunter_Data/Managed/Assembly-CSharp.dll",
    "SwapHunter_Data/Plugins/x86_64/lib_burst_generated.dll",
)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def hash_stream(stream):
    sha, length = hashlib.sha256(), 0
    while chunk := stream.read(1024 * 1024):
        sha.update(chunk)
        length += len(chunk)
    return length, sha.hexdigest()


def fingerprint(path):
    with path.open("rb") as stream:
        return hash_stream(stream)


def encode_json(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def load_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def path_key(name):
    return unicodedata.normalize("NFC", name).casefold()


def safe_parts(name):
    require(isinstance(name, str) and name and "\\" not in name and not name.startswith("/"),
            "Invalid package path: " + repr(name))
    parts = name.split("/")
    for part in parts:
        require(part not in ("", ".", "..") and not part.endswith((".", " "))
                and not RESERVED.fullmatch(part)
                and not any(ord(char) < 32 or char in '<>:"|?*' for char in part),
                "Unsafe Windows package path: " + repr(name))
    return parts


def ordinary_file(path):
    attributes = path.lstat()
    require(stat.S_ISREG(attributes.st_mode) and not stat.S_ISLNK(attributes.st_mode)
            and not getattr(attributes, "st_file_attributes", 0) & 0x400,
            "Only ordinary files may be packaged: " + str(path))


def contained(root, relative):
    parts = safe_parts(relative)
    path = root.joinpath(*parts)
    require(path.resolve().is_relative_to(root.resolve()), "Path escapes root: " + relative)
    cursor = root
    for part in parts:
        cursor /= part
        if cursor.exists():
            attributes = cursor.lstat()
            require(not stat.S_ISLNK(attributes.st_mode)
                    and not getattr(attributes, "st_file_attributes", 0) & 0x400,
                    "Link/reparse point is not accepted: " + str(cursor))
    return path


def excluded(name):
    path = PurePosixPath(name)
    parts = {part.casefold() for part in path.parts}
    return (bool(parts & PRIVATE_PARTS)
            or any("donotship" in part or "dontship" in part for part in parts)
            or path.suffix.casefold() in {".pdb", ".mdb", ".log", ".tmp", ".bak"}
            or re.fullmatch(r"(?:expedition|campaign|settings)-v\d+\.json(?:\.meta)?", path.name, re.I)
            or path.name.casefold() == "demonstration-session.json")


def checked_build(project, executable, version, commit):
    require(executable.name == "SwapHunter.exe", "Expected SwapHunter.exe")
    folder = executable.parent
    identity = load_json(folder / "build-identity.json")
    require(identity.get("version") == version and identity.get("status") == "Succeeded"
            and identity.get("sourceUnchanged") is True and identity.get("target") == "StandaloneWindows64",
            "Build must be a successful unchanged-source Windows64 build of the requested version")
    require(identity.get("gitCommit") == commit, "Build commit differs from --commit")
    actual_commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=project, text=True).strip()
    require(actual_commit == commit, "Current Git HEAD differs from --commit")
    for relative, key in (("SwapHunter.exe", "executableSha256"),
                          ("SwapHunter_Data/Managed/Assembly-CSharp.dll", "assemblySha256")):
        path = contained(folder, relative)
        ordinary_file(path)
        require(fingerprint(path)[1] == identity.get(key), "Build identity mismatch: " + relative)
    source = load_json(folder / "source-manifest.json")
    records = source.get("files")
    require(isinstance(records, list) and records, "Build input manifest is empty")
    scope = ["Game/Assets", "Game/Packages", "Game/ProjectSettings", "Dependencies"]
    require(source.get("scope") == scope, "Unexpected build source scope")
    seen = set()
    for record in records:
        name = record["path"]
        require(name not in seen and any(name.startswith(prefix + "/") for prefix in scope),
                "Duplicate or out-of-scope source input: " + name)
        seen.add(name)
        path = contained(project, name)
        ordinary_file(path)
        require(fingerprint(path)[1] == record["sha256"], "Source changed after build: " + name)
    canonical = "\n".join(record["path"] + "\t" + record["sha256"] for record in records) + "\n"
    require(hashlib.sha256(canonical.encode("utf-8")).hexdigest() == source.get("sha256")
            == identity.get("sourceSha256"), "Build input manifest hash mismatch")
    assembly_bytes = (folder / "SwapHunter_Data/Managed/Assembly-CSharp.dll").read_bytes()
    for flag in ("-swapHunterTacticalDemo", "-demoAllUnlocked", "-demoMenu"):
        require(flag.encode("utf-16le") in assembly_bytes, "Player lacks launcher flag: " + flag)
    return identity, source


def add(payload, name, path_or_bytes):
    safe_parts(name)
    require(path_key(name) not in {path_key(key) for key in payload}, "Duplicate package path: " + name)
    if isinstance(path_or_bytes, Path):
        ordinary_file(path_or_bytes)
    payload[name] = path_or_bytes


def collect_licenses(project, payload, cc_by_license):
    ledger = load_json(project / "THIRD_PARTY_ASSETS.json")
    entries = ledger.get("entries")
    require(isinstance(entries, list) and entries, "Third-party asset ledger is empty")
    files = set(BASE_NOTICES)
    for entry in entries:
        require(entry.get("name") and entry.get("license") and entry.get("sourceUrl")
                and entry.get("allowPlayer") is True, "Incomplete player redistribution record")
        notices = entry.get("licenseFiles")
        require(isinstance(notices, list) and notices, "Asset license files are missing")
        files.update(notices)
    for name in sorted(files):
        path = contained(project, name)
        require(path.is_file() and path.stat().st_size > 0, "Missing license: " + name)
        add(payload, "Licenses/" + name, path)
    add(payload, "Licenses/NotoSansSC-OFL.txt", project / "Game/Assets/_SwapHunter/Fonts/OFL.txt")
    add(payload, "THIRD_PARTY_ASSETS.md", project / "THIRD_PARTY_ASSETS.md")
    add(payload, "THIRD_PARTY_ASSETS.json", project / "THIRD_PARTY_ASSETS.json")
    if cc_by_license:
        text = cc_by_license.read_text(encoding="utf-8-sig").lower()
        require("creative commons" in text and "3.0" in text and len(text) > 5000,
                "Provided CC BY 3.0 text does not appear to be the full legal code")
        add(payload, "Licenses/CC-BY-3.0.txt", cc_by_license)
    # Copy license texts only, never package caches or editor dependencies.
    lock = load_json(project / "Game/Packages/packages-lock.json")
    cache = project / "Game/Library/PackageCache"
    package_notices = []
    for name, locked in sorted(lock["dependencies"].items()):
        if locked.get("source") not in ("registry", "builtin"):
            continue
        candidates = []
        for directory in cache.glob(name + "@*"):
            metadata = directory / "package.json"
            if metadata.is_file():
                package = load_json(metadata)
                if package.get("name") == name and package.get("version") == locked.get("version"):
                    candidates.append(directory)
        if not candidates and locked.get("source") == "builtin":
            continue  # Unity modules are engine components, not cached UPM packages.
        require(len(candidates) == 1, "Cannot identify the locked package license location: " + name)
        directory = candidates[0]
        notices = [path for path in directory.iterdir() if path.is_file()
                   and path.suffix.casefold() != ".meta"
                   and (path.name.casefold().startswith("license")
                        or "thirdparty" in path.name.casefold().replace(" ", "").replace("_", ""))]
        require(notices, "Locked package has no top-level license/notice: " + name)
        noise = directory / "Unity.Mathematics/Noise/LICENSE"
        if noise.is_file():
            notices.append(noise)
        for path in sorted(notices):
            relative = path.relative_to(directory).as_posix()
            target = "Licenses/Packages/" + name + "/" + relative
            add(payload, target, path)
            package_notices.append(target)
    return {"assetEntries": len(entries), "assetLicenseFiles": len(files),
            "packageNoticeFiles": len(package_notices), "fullCcByTextIncluded": bool(cc_by_license)}


def stage_payload(stage, payload):
    stage.mkdir()
    records = []
    for name, value in sorted(payload.items()):
        target = contained(stage, name)
        target.parent.mkdir(parents=True, exist_ok=True)
        if isinstance(value, bytes):
            original = (len(value), hashlib.sha256(value).hexdigest())
            with target.open("xb") as stream:
                stream.write(value)
        else:
            original = fingerprint(value)
            with value.open("rb") as source, target.open("xb") as destination:
                shutil.copyfileobj(source, destination, 1024 * 1024)
        require(fingerprint(target) == original, "File changed while staging: " + name)
        records.append({"path": name, "bytes": original[0], "sha256": original[1]})
    return records


def verify_archive(archive, destination, manifest_bytes):
    require(not destination.exists(), "Extraction destination already exists")
    manifest = json.loads(manifest_bytes)
    require(manifest["format"] == FORMAT, "Unsupported release manifest")
    root = manifest["archiveRoot"]
    require(len(safe_parts(root)) == 1, "Archive root must be one directory")
    expected = {}
    namespace = {}
    directories = {root}
    for row in manifest["files"] + [{"path": MANIFEST, "bytes": len(manifest_bytes),
                                     "sha256": hashlib.sha256(manifest_bytes).hexdigest()}]:
        name = root + "/" + row["path"]
        parts = safe_parts(name)
        require(type(row["bytes"]) is int and row["bytes"] >= 0 and SHA256.fullmatch(row["sha256"]),
                "Invalid manifest identity: " + name)
        require(name not in expected, "Duplicate manifest record: " + name)
        expected[name] = (row["bytes"], row["sha256"])
        for count in range(1, len(parts) + 1):
            path = "/".join(parts[:count])
            value = (path, count < len(parts))
            require(path_key(path) not in namespace or namespace[path_key(path)] == value,
                    "Manifest case/path collision: " + path)
            namespace[path_key(path)] = value
            if count < len(parts):
                directories.add(path)
    archive_before = fingerprint(archive)
    with zipfile.ZipFile(archive) as package:
        members = {}
        for info in package.infolist():
            safe_parts(info.filename)
            require(info.filename == info.orig_filename and not info.is_dir() and not info.flag_bits & 1,
                    "Unexpected ZIP entry: " + info.filename)
            mode = stat.S_IFMT(info.external_attr >> 16)
            require(mode in (0, stat.S_IFREG), "ZIP entry is not an ordinary file")
            require(info.filename not in members, "Duplicate ZIP entry")
            members[info.filename] = info
        require(set(members) == set(expected), "ZIP inventory differs from manifest")
        require(package.read(root + "/" + MANIFEST) == manifest_bytes, "Embedded manifest differs")
        for name, identity in expected.items():
            require(members[name].file_size == identity[0], "ZIP header size mismatch: " + name)
            with package.open(name) as stream:
                require(hash_stream(stream) == identity, "ZIP CRC/size/SHA mismatch: " + name)
        destination.mkdir()
        for name in sorted(expected):
            target = contained(destination, name)
            target.parent.mkdir(parents=True, exist_ok=True)
            with package.open(name) as source, target.open("xb") as output:
                shutil.copyfileobj(source, output, 1024 * 1024)
    actual_files, actual_directories = set(), set()
    for path in destination.rglob("*"):
        attributes = path.lstat()
        require(not stat.S_ISLNK(attributes.st_mode)
                and not getattr(attributes, "st_file_attributes", 0) & 0x400, "Extracted link/reparse point")
        relative = path.relative_to(destination).as_posix()
        if path.is_dir():
            actual_directories.add(relative)
        else:
            ordinary_file(path)
            actual_files.add(relative)
            require(fingerprint(path) == expected.get(relative), "Extracted size/SHA mismatch: " + relative)
    require(actual_files == set(expected) and actual_directories == directories, "Extraction inventory mismatch")
    require(fingerprint(archive) == archive_before, "Archive changed during verification")
    return {"passed": True, "archiveBytes": archive_before[0], "archiveSha256": archive_before[1],
            "payloadFiles": len(manifest["files"]), "extractedFiles": len(expected),
            "extractedRoot": str((destination / root).resolve()),
            "scope": "ZIP CRC, per-file size/SHA-256, safe independent extraction and rehash; no runtime QA."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("project", "executable", "design-doc", "output", "work"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--cc-by-license", type=Path)
    args = parser.parse_args()
    require(re.fullmatch(r"\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?", args.version), "Version must be semantic")
    require(re.fullmatch(r"[0-9a-f]{40}", args.commit), "Commit must be a full lowercase Git SHA")
    project, executable = args.project.resolve(strict=True), args.executable.resolve(strict=True)
    document = args.design_doc.resolve(strict=True)
    ordinary_file(document)
    require(document.suffix.casefold() == ".docx", "Design document must be a DOCX")
    with zipfile.ZipFile(document) as docx:
        require(docx.testzip() is None and "word/document.xml" in docx.namelist(), "Design DOCX is invalid")
    output, work = args.output.resolve(), args.work.resolve()
    require(not os.path.lexists(output) and not os.path.lexists(work), "Output and work directories must be new")
    require(not output.is_relative_to(work) and not work.is_relative_to(output), "Output/work must be separate")
    require(not output.is_relative_to(executable.parent) and not work.is_relative_to(executable.parent),
            "Package directories must not be inside the original player")
    identity, source = checked_build(project, executable, args.version, args.commit)
    payload, omitted = {}, []
    for path in sorted(executable.parent.rglob("*")):
        if path.is_dir():
            continue
        name = path.relative_to(executable.parent).as_posix()
        if excluded(name):
            omitted.append(name)
            continue
        first = safe_parts(name)[0]
        require(first in {"D3D12", "MonoBleedingEdge", "SwapHunter_Data", "SwapHunter.exe",
                          "UnityCrashHandler64.exe", "UnityPlayer.dll", "build-identity.json",
                          "source-manifest.json", "THIRD_PARTY_AUDIO_OCTOBER.txt"},
                "Unreviewed file in player root: " + name)
        add(payload, name, contained(executable.parent, name))
    require(set(NATIVE_FILES) <= payload.keys(), "Required native player files are missing")
    licenses = collect_licenses(project, payload, args.cc_by_license)
    add(payload, "Start-Preview.cmd", b'@echo off\r\nstart "" "%~dp0SwapHunter.exe" -swapHunterTacticalDemo -demoAllUnlocked -demoLevel 1 -demoMenu\r\n')
    add(payload, "Start-Campaign.cmd", b'@echo off\r\nstart "" "%~dp0SwapHunter.exe"\r\n')
    readme = f"""# 换位猎手 SwapHunter v{args.version}

Windows 64 位独立版本。完整解压后启动，不要在压缩包中直接运行。

- `Start-Preview.cmd`：全内容试玩，打开主菜单；十二章、剧情、武器与刀具使用展示预置，每次创建临时档案，退出后不保留试玩进度。
- `Start-Campaign.cmd` 或 `SwapHunter.exe`：正常成长模式，使用本机持久档案并保存进度。
- 在游戏内打开“设置”查看或调整键位；界面提示跟随自定义键位更新。

本版本包含归航行动十二章、四职业、十二把枪械、六款近战刀具，以及本轮 UI、装备、教学剧情、音效和敌人行为更新。系统设计文档作为同一 Release 的独立附件提供。

源代码提交：`{args.commit}`。版本及构建输入身份见 `build-identity.json`；逐文件完整性清单见 `release-manifest.json`。打包校验覆盖压缩包与解压文件的完整性，不等同于人工全关卡通关验收。

原始项目代码采用 MIT 许可；第三方素材及依赖保留各自许可。枪声包含 Michel Baradari 的 CC BY 3.0 录音衍生素材，换弹素材包含 SpringySpringo 的 CC0 录音；完整署名、来源、修改说明和许可文本见 `Licenses/` 与 `THIRD_PARTY_ASSETS.md`。
"""
    add(payload, "README.zh-CN.md", readme.encode("utf-8"))
    work.mkdir(parents=True)
    stage = work / "stage"
    records = stage_payload(stage, payload)
    root = "SwapHunter-v" + args.version + "-Windows-x64"
    manifest = {"format": FORMAT, "version": args.version, "archiveRoot": root, "commit": args.commit,
                "createdUtc": datetime.now(timezone.utc).isoformat(),
                "buildIdentity": {key: identity[key] for key in
                                  ("version", "gitCommit", "sourceSha256", "assemblySha256", "executableSha256")},
                "files": records}
    manifest_bytes = encode_json(manifest)
    (stage / MANIFEST).write_bytes(manifest_bytes)
    archive = work / (root + ".zip")
    with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED, compresslevel=6) as package:
        for row in records:
            package.write(stage / row["path"], root + "/" + row["path"])
        package.write(stage / MANIFEST, root + "/" + MANIFEST)
    verification = verify_archive(archive, work / "extracted", manifest_bytes)
    for name in NATIVE_FILES:
        extracted = work / "extracted" / root / name
        require(fingerprint(extracted) == fingerprint(executable.parent / name), "Native identity changed: " + name)
    staged_doc = work / ("SwapHunter-System-Design-v" + args.version + ".docx")
    design_fingerprint = fingerprint(document)
    with document.open("rb") as src, staged_doc.open("xb") as dst:
        shutil.copyfileobj(src, dst, 1024 * 1024)
    require(fingerprint(staged_doc) == design_fingerprint, "Design document changed during copy")
    output.mkdir(parents=True)
    assets = []
    for source_file in (archive, staged_doc):
        target = output / source_file.name
        expected = fingerprint(source_file)
        with source_file.open("rb") as src, target.open("xb") as dst:
            shutil.copyfileobj(src, dst, 1024 * 1024)
        require(fingerprint(target) == expected, "Final asset copy mismatch")
        assets.append({"name": target.name, "bytes": expected[0], "sha256": expected[1]})
    sums = output / "SHA256SUMS.txt"
    sums.write_text("".join(asset["sha256"] + "  " + asset["name"] + "\n" for asset in assets), encoding="utf-8")
    assets.append({"name": sums.name, "bytes": sums.stat().st_size, "sha256": fingerprint(sums)[1]})
    report = {"passed": True, "version": args.version, "commit": args.commit, "assets": assets,
              "sourceManifestFilesVerified": len(source["files"]), "sourceDocumentSha256": design_fingerprint[1],
              "licenses": licenses, "omittedPlayerFiles": omitted, "verification": verification,
              "runtimeValidation": "not_run_by_packager", "remotePublication": "not_performed"}
    (work / "package-verification.json").write_bytes(encode_json(report))
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, KeyError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print("Release packaging failed; original and partial new files are preserved: " + str(error), file=sys.stderr)
        raise SystemExit(1)
