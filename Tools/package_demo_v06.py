"""Create local v0.6 packages from one identified build and its actual QA evidence.

No Unity/player/Blender execution, network access, publication or Git mutation.
Release mode is the default and requires all six native gates, including 12 routes.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import zipfile

from verify_package_v06 import verify_archive

VERSION = "0.6.0"
REQUIRED_SUITES = ("Core", "Missions", "Routes", "Storage", "Gunplay", "Visual")
ADDITIONAL_SUITES = ("Pool", "Lifecycle", "BossEscape")
EXPECTED_REPORTS = {
    "Core": "core-v06-report.json", "Missions": "mission-v06-report.json",
    "Routes": "route-v06-report.json", "Storage": "storage-v06-report.json",
    "Gunplay": "gunplay-presentation-report.json", "Visual": "visual-v06-report.json",
    "Performance": "performance-v06-report.json", "Allocation": "allocation-v06-report.json",
    "Pool": "pool-v06-report.json", "Lifecycle": "lifecycle-v06-report.json", "BossEscape": "boss-escape-v06-report.json",
}
AUTHOR_NAMES = ("combat-knife-v05", "operative-arms-v04", "robot-assault-v04", "robot-commander-v04",
                "robot-shield-v04", "robot-sniper-v04") + tuple(f"weapon_{i:02}" for i in range(2, 12))
THREE_NAMES = ("assault-sentinel", "bulkhead-module", "cargo-container", "commander-sentinel",
               "control-terminal", "cx24-carbine", "deck-module", "operative-arms", "operative-body",
               "phase-beacon", "s12-scattergun", "security-gate", "shield-sentinel", "sniper-sentinel", "supply-case")
REQUIRED_SOURCE = (
    "Game/README.md", "Tools/Start-Demo-v06.ps1", "Tools/Package-Demo-v06.ps1",
    "Tools/package_demo_v06.py", "Tools/Verify-Package-v06.ps1", "Tools/verify_package_v06.py",
    "Tools/ArtAuthoring/tactical-weapons-v052.mjs", "Tools/ArtAuthoring/generate-base-weapons-v052.mjs",
    "Tools/ArtAuthoring/base-weapons-v052-manifest.json",
)
EXCLUDED_PARTS = {".git", ".codex", ".agents", "library", "logs", "builds", "temp", "obj", "node_modules",
                  "usersettings", "memorycaptures", "recordings", ".vs", ".idea", ".vscode", "__pycache__",
                  "swaphunter_plan", "environmentcheck", "interview-notes", "work", "outputs"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    value = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def captured_json(path):
    raw = Path(path).read_bytes()
    return json.loads(raw.decode("utf-8-sig")), raw


def safe_relative(value):
    require(isinstance(value, str) and value and "\\" not in value and ":" not in value and "\x00" not in value, "Invalid relative path: " + repr(value))
    parts = value.split("/")
    require(not value.startswith("/") and all(part not in ("", ".", "..") for part in parts), "Unsafe relative path: " + value)
    return PurePosixPath(value)


def contained(root, relative):
    relative = safe_relative(relative)
    target = root.joinpath(*relative.parts)
    require(target.resolve().is_relative_to(root.resolve()) and not target.is_symlink(), "Path escapes its declared root: " + str(relative))
    return target


def identified_build(executable):
    executable = executable.resolve(strict=True)
    folder = executable.parent
    identity = read_json(folder / "build-identity.json")
    require(identity.get("version") == VERSION and identity.get("status") == "Succeeded" and identity.get("sourceUnchanged") is True,
            "Build identity must be a successful v0.6.0 build with unchanged source inputs")
    assembly = folder / (executable.stem + "_Data/Managed/Assembly-CSharp.dll")
    require(digest(executable) == identity.get("executableSha256", "").lower(), "Executable differs from build identity")
    require(digest(assembly) == identity.get("assemblySha256", "").lower(), "Assembly differs from build identity")
    source = read_json(folder / "source-manifest.json")
    records = source.get("files")
    require(isinstance(records, list) and records, "Build source-manifest is missing its file records")
    legacy_scope = ["Game/Assets", "Game/Packages", "Game/ProjectSettings"]
    require(source.get("scope") in (legacy_scope, legacy_scope + ["Dependencies"]),
            "Unexpected build-input scope; legacy identities remain unchanged")
    paths = []
    for record in records:
        name = str(safe_relative(record["path"]))
        require(any(name.startswith(scope + "/") for scope in source["scope"]), "Build input outside declared source scope")
        paths.append(name)
    require(len(paths) == len(set(paths)), "Duplicate source-manifest path")
    canonical = "\n".join(record["path"] + "\t" + record["sha256"] for record in records) + "\n"
    calculated = hashlib.sha256(canonical.encode("utf-8")).hexdigest()
    require(calculated == source.get("sha256") == identity.get("sourceSha256"), "Build source-manifest identity mismatch")
    require((folder / "UnityPlayer.dll").is_file() and (folder / "MonoBleedingEdge").is_dir(), "Incomplete Windows Mono player")
    return identity, source, assembly


def matching_identity(candidate, expected, label):
    for key in ("version", "sourceSha256", "assemblySha256", "executableSha256"):
        require(candidate.get(key) == expected.get(key), label + " has a different " + key)
    require(candidate.get("status") == "Succeeded" and candidate.get("sourceUnchanged") is True, label + " is not an accepted build")


def route_full_coverage(report):
    chapters = report.get("chapters", [])
    selected = report.get("selectedLevels", [])
    return (report.get("allTwelveCovered") is True and report.get("passed") is True
            and isinstance(selected, list) and len(selected) == 12 and all(type(level) is int for level in selected)
            and set(selected) == set(range(1, 13)) and isinstance(chapters, list)
            and len(chapters) == 12 and all(isinstance(chapter, dict) and type(chapter.get("level")) is int for chapter in chapters)
            and {chapter.get("level") for chapter in chapters} == set(range(1, 13))
            and all(chapter.get("passed") is True for chapter in chapters))


def collect_validation(directories, identity):
    records = {}
    evidence = []
    for run_index, directory in enumerate(directories, 1):
        directory = directory.resolve(strict=True)
        captured = {filename: captured_json(directory / filename) for filename in
                    ("build-identity.json", "assembly-sha256.json", "run-config.json", "summary.json")}
        matching_identity(captured["build-identity.json"][0], identity, "Validation directory")
        assembly_record = captured["assembly-sha256.json"][0]
        require(assembly_record.get("Hash", "").lower() == identity["assemblySha256"].lower(), "Validation assembly hash differs")
        config = captured["run-config.json"][0]
        for key in ("sourceSha256", "assemblySha256"):
            require(config.get(key) == identity[key], "Validation configuration identity differs")
        rows = captured["summary.json"][0]
        require(isinstance(rows, list) and rows, "Validation summary must contain actual case results")
        for filename in ("build-identity.json", "assembly-sha256.json", "run-config.json", "summary.json"):
            evidence.append((f"run-{run_index:02}/{filename}", captured[filename][1]))
        for row in rows:
            suite = row.get("suite")
            if suite not in EXPECTED_REPORTS:
                continue  # Legacy never satisfies a v0.6 gate.
            require(suite not in records, "Duplicate " + suite + " evidence; select one final run explicitly instead of hiding a failed retry")
            for key in ("sourceSha256", "assemblySha256"):
                require(row.get(key) == identity[key], "Case identity differs: " + str(suite))
            relative = row.get("report", "")
            report_path = contained(directory, relative)
            require(report_path.name == EXPECTED_REPORTS[suite], "Unexpected report for " + suite)
            report, report_raw = captured_json(report_path)
            require(report.get("version") == VERSION, "Native report version differs: " + suite)
            passed = row.get("passed") is True and type(row.get("exitCode")) is int and row["exitCode"] == 0 and row.get("timedOut") is not True and report.get("passed") is True
            checks = report.get("checks", [])
            if suite in REQUIRED_SUITES or suite in ("Pool", "Lifecycle"):
                passed = passed and isinstance(checks, list) and bool(checks) and all(check.get("passed") is True for check in checks)
                passed = passed and type(row.get("checks")) is int and row["checks"] == len(checks)
            entry = {"status": "passed" if passed else "failed", "name": row.get("name"), "checks": len(checks),
                     "displayMode": row.get("displayMode"), "reportSha256": hashlib.sha256(report_raw).hexdigest(),
                     "evidence": f"run-{run_index:02}/{relative}"}
            if suite in ADDITIONAL_SUITES:
                entry["scope"] = report.get("scope", report.get("kind", ""))
            if suite == "BossEscape":
                attempts = report.get("attempts", [])
                expected = report.get("expectedEscapeAttempts", 0)
                trajectory = report_path.parent / "boss-escape-v06-trajectory.csv"
                passed = passed and report.get("complete") is True and isinstance(attempts, list) and type(expected) is int and expected > 0
                passed = passed and len(attempts) == expected + 1 and all(attempt.get("passed") is True for attempt in attempts)
                passed = passed and type(row.get("checks")) is int and row["checks"] == len(attempts) and trajectory.is_file() and trajectory.stat().st_size > 0
                entry.update(checks=len(attempts), expectedEscapeAttempts=expected, movement=report.get("movement"),
                             reactionDelaySeconds=report.get("reactionDelaySeconds"), loadout=report.get("loadout"))
                if trajectory.is_file():
                    evidence.append((f"run-{run_index:02}/{trajectory.relative_to(directory).as_posix()}", trajectory))
            elif suite == "Lifecycle":
                passed = passed and report.get("warmupCycles", 0) > 0 and report.get("measuredCycles", 0) > 0
                passed = passed and report.get("completedWarmups") == report.get("warmupCycles") and report.get("completedMeasured") == report.get("measuredCycles")
                entry.update(completedWarmups=report.get("completedWarmups"), completedMeasured=report.get("completedMeasured"),
                             ownership=report.get("ownership"), globalCounts=report.get("globalCounts"))
            elif suite == "Routes":
                entry["allTwelveCovered"] = route_full_coverage(report)
                entry["selectedLevels"] = report.get("selectedLevels", [])
                entry["scope"] = report.get("scope", "")
            elif suite == "Visual":
                frames = report.get("frames", [])
                passed = passed and isinstance(frames, list) and bool(frames)
                for frame in frames:
                    image_path = contained(report_path.parent, frame.get("file", ""))
                    require(image_path.is_file() and image_path.stat().st_size > 0, "Visual evidence image missing")
                    image_relative = image_path.relative_to(directory).as_posix()
                    evidence.append((f"run-{run_index:02}/{image_relative}", image_path))
                entry["frames"] = len(frames)
                entry["humanVisualReviewCompleted"] = report.get("humanVisualReviewCompleted") is True
                entry["scope"] = report.get("kind", "")
            elif suite == "Performance":
                csv_path = report_path.parent / "performance-v06-frames.csv"
                passed = passed and row.get("displayMode") == "Foreground" and report.get("batchMode") is False
                passed = passed and report.get("measurementValid") is True and report.get("frames", 0) > 0 and report.get("focusFraction") == 1 and csv_path.is_file()
                entry.update(measurementValid=report.get("measurementValid") is True, frames=report.get("frames", 0), focusFraction=report.get("focusFraction"),
                             scope="Measured foreground fixture only; no cross-hardware or improvement claim")
                if csv_path.is_file():
                    evidence.append((f"run-{run_index:02}/{csv_path.relative_to(directory).as_posix()}", csv_path))
            elif suite == "Allocation":
                # Match the current native validator; unavailable byte counters are never called zero bytes.
                passed = passed and report.get("measurementValid") is True and report.get("objectLifecycleMeasurementValid") is True
                passed = passed and report.get("completedBatches", 0) > 0 and report.get("completedBatches") == report.get("measuredBatches")
                passed = passed and report.get("retiredDamageProbePassed") is True and report.get("totalProductionRetired") == report.get("totalSuccessfulSpawnCalls")
                entry.update(byteMeasurementComplete=report.get("byteMeasurementComplete") is True,
                             eventMeasurementComplete=report.get("eventMeasurementComplete") is True, outcome=report.get("outcome"))
                entry["scope"] = "Synchronous managed allocation/object-lifecycle diagnostic; byte/event availability stays separate; not FPS evidence"
            entry["status"] = ("partial" if suite == "Routes" and passed and not entry["allTwelveCovered"]
                               else "passed" if passed else "failed")
            records[suite] = entry
            evidence.append((f"run-{run_index:02}/{relative}", report_raw))
    gates = {suite: records.get(suite, {"status": "not_run"}) for suite in REQUIRED_SUITES}
    ready = all(gate["status"] == "passed" for gate in gates.values()) and gates["Routes"].get("allTwelveCovered") is True
    return {"releaseGatePassed": ready, "gates": gates,
            "additionalChecks": {suite: records.get(suite, {"status": "not_run"}) for suite in ADDITIONAL_SUITES},
            "performance": records.get("Performance", {"status": "not_run", "scope": "No foreground measurement supplied"}),
            "allocation": records.get("Allocation", {"status": "not_run"}),
            "humanTesting": {"status": "not_assessed", "note": "Packaging does not perform or certify human playtesting"}}, evidence


def excluded_source(relative):
    path = safe_relative(relative)
    lowered = [part.casefold() for part in path.parts]
    return (any(part in EXCLUDED_PARTS for part in lowered) or path.name.casefold() == "agents.md"
            or path.name.startswith(".env") or path.suffix.casefold() in {".log", ".tmp", ".bak", ".pyc", ".blend1", ".blend2", ".pfx", ".p12", ".key"})


def expected_assets():
    blends = {"Tools/BlenderAuthoring/blend/" + name + ".blend" for name in AUTHOR_NAMES + ("weapon-lineup",)}
    models = {"Game/Assets/_SwapHunter/Art/BlenderModels/" + name + ".glb" for name in AUTHOR_NAMES}
    models |= {"Game/Assets/_SwapHunter/Art/ThreeModels/" + name + ".glb" for name in THREE_NAMES}
    return blends, models


def collect_source(project, build_source):
    require((project / ".git").exists(), "Source packaging requires the authoritative Git checkout; a ZIP is verified/rebuilt separately")
    result = subprocess.run(["git", "-C", str(project), "ls-files", "--cached", "--others", "--exclude-standard", "-z"], capture_output=True, check=True)
    names = sorted(set(name for name in result.stdout.decode("utf-8").split("\0") if name))
    selected = {}
    for name in names:
        if excluded_source(name):
            continue
        path = contained(project, name)
        if path.is_file():
            selected[name] = path
    blends, models = expected_assets()
    required = set(REQUIRED_SOURCE) | blends | models | {name + ".meta" for name in models}
    require(required <= selected.keys(), "Missing/ignored source or author asset: " + ", ".join(sorted(required - selected.keys())))
    for record in build_source["files"]:
        name = str(safe_relative(record["path"]))
        require(name in selected, "Build input omitted from clean source archive: " + name)
        require(digest(selected[name]) == record["sha256"], "Current source differs from validated build input: " + name)
    # A legacy manifest remains readable, but cannot certify an unrecorded local package.
    package_manifest = project / "Game/Packages/manifest.json"
    for package, reference in read_json(package_manifest).get("dependencies", {}).items():
        if not isinstance(reference, str) or not reference.startswith("file:"):
            continue
        local = (package_manifest.parent / reference[5:]).resolve()
        require(local.is_relative_to(project.resolve()) and local.is_dir(),
                "Local dependency is outside the public project or missing: " + package)
        relative = local.relative_to(project.resolve()).as_posix()
        require(any(relative == scope or relative.startswith(scope + "/") for scope in build_source["scope"]),
                "Local dependency was omitted from this build identity; rebuild with the expanded input scope: " + package)
    # New build inputs added since compilation must not silently enter the source package.
    expected_inputs = {record["path"] for record in build_source["files"]}
    current_inputs = {path.relative_to(project).as_posix() for folder in build_source["scope"]
                      for path in (project / folder).rglob("*") if path.is_file()}
    require(current_inputs == expected_inputs, "Build input set changed after compilation; rebuild before packaging")
    head = subprocess.run(["git", "-C", str(project), "rev-parse", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()
    dirty = bool(subprocess.run(["git", "-C", str(project), "status", "--porcelain=v1", "--untracked-files=all"], capture_output=True, text=True, check=True).stdout.strip())
    return selected, {"baseGitCommit": head, "workingTreeDirty": dirty, "remotePublication": "not_claimed",
                      "requiredBlendFiles": len(blends), "requiredGlbFiles": len(models),
                      "includedBlendFiles": sum(name.endswith(".blend") for name in selected),
                      "includedGlbFiles": sum(name.endswith(".glb") for name in selected)}


def add(payload, name, value):
    safe_relative(name)
    require(name not in payload and name.casefold() not in {key.casefold() for key in payload}, "Package path collision: " + name)
    payload[name] = value


def manifest_for(payload, kind, archive_root, identity):
    files = []
    for name, value in sorted(payload.items()):
        if isinstance(value, bytes):
            size, sha = len(value), hashlib.sha256(value).hexdigest()
        else:
            size, sha = value.stat().st_size, digest(value)
        files.append({"path": name, "bytes": size, "sha256": sha})
    return {"schemaVersion": 1, "artifact": kind, "version": VERSION, "archiveRoot": archive_root,
            "identity": {key: identity[key] for key in ("sourceSha256", "assemblySha256", "executableSha256")}, "files": files}


def write_archive(path, payload, manifest):
    # ZIP mode x and a new output directory preserve all earlier deliveries.
    with zipfile.ZipFile(path, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        prefix = manifest["archiveRoot"] + "/"
        for name, value in sorted(payload.items()):
            if isinstance(value, bytes):
                archive.writestr(prefix + name, value)
            else:
                archive.write(value, prefix + name)
        archive.writestr(prefix + "package-manifest.json", json_bytes(manifest))


def verify_windows_manifest_identity(manifest, identity, source, executable_name):
    records = {record["path"]: record for record in manifest["files"]}
    expected = {
        executable_name: identity["executableSha256"],
        Path(executable_name).stem + "_Data/Managed/Assembly-CSharp.dll": identity["assemblySha256"],
        "build-identity.json": hashlib.sha256(json_bytes(identity)).hexdigest(),
        "source-manifest.json": hashlib.sha256(json_bytes(source)).hexdigest(),
    }
    for name, sha in expected.items():
        require(records.get(name, {}).get("sha256") == sha, "Player identity changed while preparing package: " + name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--executable", required=True, type=Path)
    parser.add_argument("--validation-dir", action="append", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--verification-root", type=Path)
    parser.add_argument("--mode", choices=("release", "candidate"), default="release")
    parser.add_argument("--check-only", action="store_true", help="Check gates and file selection without creating archives or output directories")
    args = parser.parse_args()
    project, executable, output = args.project.resolve(strict=True), args.executable.resolve(strict=True), args.output.resolve()
    verify_root = args.verification_root.resolve() if args.verification_root else output / "verification"
    require(not output.exists() and not verify_root.exists(), "Output/verification directories must be new; no existing data will be overwritten")
    require(not output.is_relative_to(verify_root), "Verification root cannot equal or be an ancestor of the new delivery directory")
    require(not output.is_relative_to(project) and not verify_root.is_relative_to(project), "Use delivery/verification directories outside the source checkout")
    require(not output.is_relative_to(executable.parent) and not verify_root.is_relative_to(executable.parent), "Do not put packages or extraction inside the original player")
    identity, build_source, assembly = identified_build(executable)
    validation, evidence = collect_validation(args.validation_dir, identity)
    require(args.mode != "release" or validation["releaseGatePassed"], "Formal packaging rejected: Core/Missions/Storage/Gunplay/Visual and all 12 Routes must pass for this exact build")
    source_payload, source_info = collect_source(project, build_source)
    from check_project_v06 import check_project
    static = check_project(project)
    require(static.passed, "Static source gate failed: " + "; ".join(check["name"] for check in static.results if not check["passed"]))

    windows_payload = {}
    for path in sorted(executable.parent.rglob("*")):
        relative = path.relative_to(executable.parent).as_posix()
        if any("DoNotShip" in part or "BackUpThisFolder_ButDontShip" in part for part in path.parts):
            continue
        if path.is_file():
            require(path.resolve().is_relative_to(executable.parent) and not path.is_symlink(), "Player contains an external link")
            add(windows_payload, relative, path)
    # Freeze accepted identity metadata; never silently package later edits to it.
    windows_payload["build-identity.json"] = json_bytes(identity)
    windows_payload["source-manifest.json"] = json_bytes(build_source)
    marker = "SWAPHUNTER_ISOLATED_DEMO_V1".encode("utf-16le")
    demo_supported = marker in assembly.read_bytes()
    if demo_supported:
        add(windows_payload, "Start-Demo-v06.ps1", project / "Tools/Start-Demo-v06.ps1")
        launcher = '''<# 手动运行：独立演示档；不读取正式成长。可用 -Level 选择 1..12 关。 #>\n[CmdletBinding(SupportsShouldProcess=$true)]\nparam([ValidateRange(1,12)][int]$Level=2)\n& (Join-Path $PSScriptRoot 'Start-Demo-v06.ps1') -ExecutablePath (Join-Path $PSScriptRoot 'SwapHunter.exe') -Level $Level -WhatIf:$WhatIfPreference\n'''
        require(executable.name == "SwapHunter.exe", "Packaged demo launcher expects the project's SwapHunter.exe filename")
        add(windows_payload, "开始演示.ps1", b"\xef\xbb\xbf" + launcher.encode("utf-8"))
    for name in ("LICENSE", "THIRD_PARTY_NOTICES.md", "Game/THIRD_PARTY_NOTICES.md",
                 "Game/Assets/_SwapHunter/Fonts/OFL.txt", "Game/Assets/_SwapHunter/Art/ThreeModels/THREE-LICENSE.txt"):
        require(name in source_payload, "Licence notice omitted from source selection: " + name)
        add(windows_payload, "Licenses/" + name, source_payload[name])
    state = "正式软件门禁通过" if args.mode == "release" else "候选包：未标记为正式交付，各项实际结果分别列出"
    gate_lines = "\n".join("- " + suite + "：" + validation["gates"][suite]["status"] + ("；十二关=" + str(validation["gates"][suite].get("allTwelveCovered", False)) if suite == "Routes" else "") for suite in REQUIRED_SUITES)
    demo_text = "手动运行 `开始演示.ps1 -Level 2` 可打开有持续标识的独立演示档；全武器与飞刀为明确预置，不是通关记录。" if demo_supported else "此构建未识别到独立演示能力标记，因此未附演示启动器。"
    readme = f'''# SwapHunter v{VERSION} 本地交付\n\n{state}。\n\n{gate_lines}\n\nRoutes 是清敌/耗尽跳板的保底路线夹具，不等于真人或真实战斗通关。Visual 是原生离屏图像与程序检查，主观画面评审另行记录。\n\n性能：{validation["performance"]["status"]}；未执行不能记为通过，单次采样不证明提升或跨硬件表现。真人体验：本打包工具未执行或认证真人测试。\n\n## 运行\n\n完整解压 Windows ZIP 后双击 `SwapHunter.exe`，保留整个目录。{demo_text}\n\n## 源码与复现\n\nSource ZIP 包含 Git 跟踪文件和未忽略的新源码，以及至少 17 份 Blender 原稿、31 个 GLB 和 Unity meta。未打包 Git 数据、缓存、Logs/Builds 或内部求职材料。基准 Git 提交：`{source_info["baseGitCommit"]}`；工作树有未提交变化：`{source_info["workingTreeDirty"]}`。这不是已推送远端的声明；实际源码快照以 Source ZIP 的 SHA-256 为准。\n\n构建输入 SHA-256：`{identity["sourceSha256"]}`；程序集 SHA-256：`{identity["assemblySha256"]}`。\n\n`package-manifest.json` 列出归档内所有有效负载（不含清单自身）。工具逐项回读 ZIP，再写入全新目录独立解压并核验；完整性通过不等于已经在干净目录重新构建。干净复现时用 Source 根目录的 `Tools/Check-Project-v06.ps1`、`Tools/Build-Demo-v06.ps1` 和 `Tools/Validate-Demo-v06.ps1` 生成新的真实证据，普通构建不调用 Prepare。\n\n不要用旧包的报告替代新程序集结果；保留原失败报告和旧版本。\n'''
    status = {"schemaVersion": 1, "mode": args.mode, "version": VERSION, "buildIdentity": identity,
              "validation": validation, "sourceSnapshot": source_info, "demoLauncherIncluded": demo_supported,
              "cleanRebuild": {"status": "not_run_by_packager"}, "createdUtc": datetime.now(timezone.utc).isoformat()}
    readme += "\n原始验证 JSON、Visual 图片及已提供的性能 CSV 位于交付目录的 `evidence/`，不嵌入两个 ZIP；ZIP 内保存验证摘要。既有失败报告继续留在原验证目录。\n"
    readme += "\n附加检查（独立于原六个门禁）：" + "；".join(suite + "=" + validation["additionalChecks"][suite]["status"] for suite in ADDITIONAL_SUITES) + "。各项原始报告同样保留；未运行和失败不标为通过。\n"
    for payload in (windows_payload, source_payload):
        add(payload, "开始阅读.md", readme.encode("utf-8"))
        add(payload, "validation-status.json", json_bytes(status))
    base = "SwapHunter-v" + VERSION + ("-candidate" if args.mode == "candidate" else "")
    windows_manifest = manifest_for(windows_payload, "windows", base + "-Windows", identity)
    verify_windows_manifest_identity(windows_manifest, identity, build_source, executable.name)
    source_manifest = manifest_for(source_payload, "source", base + "-Source", identity)
    source_records = {record["path"]: record for record in source_manifest["files"]}
    for record in build_source["files"]:
        require(source_records[record["path"]]["sha256"] == record["sha256"], "Source changed while preparing package manifests; rebuild/retry")
    evidence_records = [{"path": relative,
                         "sha256": hashlib.sha256(value).hexdigest() if isinstance(value, bytes) else digest(value),
                         "bytes": len(value) if isinstance(value, bytes) else value.stat().st_size} for relative, value in evidence]
    evidence_hashes = {record["path"]: record["sha256"] for record in evidence_records}
    for gate in [*validation["gates"].values(), *validation["additionalChecks"].values(), validation["performance"], validation["allocation"]]:
        if "evidence" in gate:
            require(evidence_hashes[gate["evidence"]] == gate["reportSha256"], "Validation report changed while preparing package")
    if args.check_only:
        print(json.dumps({"mode": args.mode, "checkOnly": True, "releaseGatePassed": validation["releaseGatePassed"],
                          "sourceFiles": len(source_manifest["files"]), "windowsFiles": len(windows_manifest["files"]),
                          "sourceSnapshot": source_info, "validation": validation}, ensure_ascii=False, indent=2))
        return 0

    # All semantic gates finish before creating the new delivery directory.
    output.mkdir(parents=True, exist_ok=False)
    (output / "manifests").mkdir()
    index = readme + f'\n## 文件\n\n- [Windows 玩家包]({base}-Windows.zip)\n- [源码与作者原稿]({base}-Source.zip)\n- [包哈希](SHA256SUMS.txt)\n- [打包和独立解压记录](package-report.json)\n'
    (output / "开始阅读.md").write_bytes(index.encode("utf-8"))
    (output / "validation-status.json").write_bytes(json_bytes(status))
    for (relative, value), expected in zip(evidence, evidence_records):
        destination = output / "evidence" / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open("xb") as target:
            if isinstance(value, bytes):
                target.write(value)
            else:
                with value.open("rb") as original:
                    shutil.copyfileobj(original, target)
        require(digest(destination) == expected["sha256"], "Evidence changed during copy; partial output preserved")
    (output / "evidence-files.json").write_bytes(json_bytes(evidence_records))
    verify_root.mkdir(parents=True, exist_ok=False)
    artifacts = []
    for kind, payload, manifest in (("windows", windows_payload, windows_manifest), ("source", source_payload, source_manifest)):
        archive = output / (manifest["archiveRoot"] + ".zip")
        manifest_path = output / "manifests" / (kind + "-manifest.json")
        manifest_path.write_bytes(json_bytes(manifest))
        write_archive(archive, payload, manifest)
        result = verify_archive(archive, verify_root / kind, manifest_path)
        artifacts.append({"kind": kind, "file": archive.name, "sha256": result["archiveSha256"], "bytes": archive.stat().st_size,
                          "files": result["fileCount"], "independentExtraction": result})
    status["artifacts"] = artifacts
    status["sourceSnapshot"] = dict(source_info, sourceArchiveSha256=next(item["sha256"] for item in artifacts if item["kind"] == "source"))
    (output / "package-report.json").write_bytes(json_bytes(status))
    (output / "SHA256SUMS.txt").write_text("\n".join(item["sha256"] + "  " + item["file"] for item in artifacts) + "\n", encoding="utf-8")
    print(json.dumps({"mode": args.mode, "releaseGatePassed": validation["releaseGatePassed"], "artifacts": artifacts}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, KeyError, subprocess.CalledProcessError, zipfile.BadZipFile) as error:
        print("Packaging failed; originals and partial new evidence were preserved: " + str(error), file=sys.stderr)
        raise SystemExit(1)
