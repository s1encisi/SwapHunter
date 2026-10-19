"""Build local Windows/Source packages from one identified player.
Profiles: Tactical/Models with routes, Synergy with UI, StoryD2 with UI, or MapV2 with UI.
Does not execute Unity, players, downloads, publishing, or old full-suite gates.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sys
sys.dont_write_bytecode = True
os.environ["GIT_OPTIONAL_LOCKS"] = "0"
from package_demo_v06 import (
    add, captured_json, collect_source, contained, digest, excluded_source,
    identified_build, json_bytes, manifest_for, matching_identity, read_json,
    require, safe_relative, verify_windows_manifest_identity, write_archive,
)
from verify_package_v06 import verify_archive

REPORTS = {
    "MapV2": "map-v2-report.json",
    "StoryD2": "story-d2-report.json",
    "Synergy": "synergy-report.json",
    "Tactical": "tactical-campaign-report.json", "Models": "models-final-report.json", "Routes": "route-v06-report.json",
    "Visual": "visual-v06-report.json", "Performance": "performance-v06-report.json",
    "Allocation": "allocation-v06-report.json",
}
REQUIRED_ROUTES = {5, 7, 12}
PRIVATE_PROFILE = re.compile(r"(?:expedition|campaign|settings)-v\d+\.json(?:\.meta)?$", re.I)
SOURCE_TOOLS = (
    "Tools/package_tactical.py", "Tools/Package-Tactical.ps1", "Tools/preview_tactical_migration.py",
    "docs/tactical-delivery-template.zh-CN.md", "docs/tactical-economy.zh-CN.md",
    "THIRD_PARTY_ASSETS.md", "THIRD_PARTY_ASSETS.json",
)
WINDOWS_DOCS = (
    "docs/tactical-playguide.zh-CN.md", "docs/tactical-design.zh-CN.md",
    "docs/tactical-interview.zh-CN.md", "docs/tactical-validation.zh-CN.md",
    "docs/tactical-economy.zh-CN.md",
)
BASE_NOTICES = (
    "LICENSE", "THIRD_PARTY_NOTICES.md", "Game/THIRD_PARTY_NOTICES.md",
    "Game/Assets/_SwapHunter/Fonts/OFL.txt",
    "Game/Assets/_SwapHunter/Art/ThreeModels/THREE-LICENSE.txt",
)


def private_file(name):
    path = safe_relative(name)
    return (excluded_source(name) or "unityeditors" in {p.casefold() for p in path.parts}
            or PRIVATE_PROFILE.fullmatch(path.name) is not None
            or path.name == "demonstration-session.json")


def add_same(payload, name, value):
    if name in payload:
        old = payload[name]
        old_hash = hashlib.sha256(old).hexdigest() if isinstance(old, bytes) else digest(old)
        new_hash = hashlib.sha256(value).hexdigest() if isinstance(value, bytes) else digest(value)
        require(old_hash == new_hash, "Conflicting existing package file: " + name)
    else:
        add(payload, name, value)


def checks_pass(row, report):
    checks = report.get("checks")
    return (row.get("passed") is True and type(row.get("exitCode")) is int
            and row["exitCode"] == 0 and row.get("timedOut") is not True
            and report.get("passed") is True and isinstance(checks, list) and bool(checks)
            and all(isinstance(c, dict) and c.get("passed") is True for c in checks)
            and type(row.get("checks")) is int and row["checks"] == len(checks))


def collect_evidence(directories, identity, ui_review, profile="tactical"):
    require(profile in ("tactical", "models", "synergy", "storyd2", "mapv2"), "Unknown packaging profile")
    evidence, records, route_records = {}, {}, []
    levels = set()
    for run_index, directory in enumerate(directories, 1):
        directory = directory.resolve(strict=True)
        prefix = f"run-{run_index:02}/"
        captured = {name: captured_json(directory / name) for name in
                    ("build-identity.json", "assembly-sha256.json", "run-config.json", "summary.json")}
        matching_identity(captured["build-identity.json"][0], identity, "Validation directory")
        require(captured["assembly-sha256.json"][0].get("Hash", "").lower() == identity["assemblySha256"].lower(),
                "Validation assembly hash differs")
        for key in ("sourceSha256", "assemblySha256"):
            require(captured["run-config.json"][0].get(key) == identity[key], "Validation config differs: " + key)
        rows = captured["summary.json"][0]
        require(isinstance(rows, list) and rows, "Validation summary has no actual cases")
        for name, (_, raw) in captured.items():
            add(evidence, prefix + name, raw)
        for row in rows:
            suite = row.get("suite")
            if suite not in REPORTS:
                continue
            require(suite == "Routes" or suite not in records,
                    "Duplicate selected " + suite + " result; explicitly choose the final run")
            for key in ("sourceSha256", "assemblySha256"):
                require(row.get(key) == identity[key], "Case identity differs: " + suite)
            relative = row.get("report", "")
            report_path = contained(directory, relative)
            require(report_path.name == REPORTS[suite], "Unexpected report filename for " + suite)
            report, raw = captured_json(report_path)
            if "version" in report or suite == "Models":
                require(report.get("version") == identity["version"], "Report version differs")
            passed = checks_pass(row, report)
            if suite in ("StoryD2", "MapV2"):
                passed = passed and report.get("completed") is True
            entry = {"status": "passed" if passed else "failed", "evidence": prefix + relative,
                     "checks": len(report.get("checks", [])), "displayMode": row.get("displayMode"),
                     "scope": report.get("scope", report.get("kind", "")),
                     "reportSha256": hashlib.sha256(raw).hexdigest()}
            add(evidence, prefix + relative, raw)
            if suite == "Routes":
                chapters, selected = report.get("chapters"), report.get("selectedLevels")
                require(isinstance(chapters, list) and chapters and isinstance(selected, list),
                        "Routes lacks actual chapter records")
                chapter_ids = [c.get("level") for c in chapters]
                require(all(type(n) is int and 1 <= n <= 12 for n in selected)
                        and len(selected) == len(set(selected)) and set(chapter_ids) == set(selected)
                        and len(chapter_ids) == len(selected), "Routes selection/completed chapter records disagree")
                require(not levels.intersection(selected), "Duplicate route chapter evidence; select final runs only")
                actual = all(c.get("passed") is True and not c.get("failure")
                             and c.get("metersWalked", 0) > 0 and c.get("seconds", 0) > 0
                             and c.get("checkpointsSkipped") == 0 for c in chapters)
                entry.update(status="passed" if passed and actual else "failed",
                             selectedLevels=selected, chapters=chapters)
                levels.update(selected)
                route_records.append(entry)
            elif suite == "Visual":
                frames = report.get("frames", [])
                require(isinstance(frames, list), "Visual frames must be a list")
                for frame in frames:
                    image = contained(report_path.parent, frame.get("file", ""))
                    require(image.is_file() and image.stat().st_size > 0, "Missing native visual frame")
                    add(evidence, prefix + image.relative_to(directory).as_posix(), image)
                entry["status"] = "passed" if passed and bool(frames) else "failed"
                entry["frames"] = len(frames)
                entry["scope"] = "Native offscreen fixture only; does not certify full UI or human review"
                records[suite] = entry
            elif suite == "Performance":
                csv = report_path.parent / "performance-v06-frames.csv"
                valid = (row.get("passed") is True and row.get("exitCode") == 0
                         and row.get("timedOut") is not True and report.get("passed") is True
                         and row.get("displayMode") == "Foreground" and report.get("batchMode") is False
                         and report.get("measurementValid") is True and report.get("frames", 0) > 0
                         and report.get("focusFraction") == 1 and csv.is_file())
                entry.update(status="measured" if valid else "invalid", frames=report.get("frames"),
                             measurementValid=valid, scope="Supplied foreground sample only; no FPS improvement or cross-device claim")
                if csv.is_file():
                    add(evidence, prefix + csv.relative_to(directory).as_posix(), csv)
                records[suite] = entry
            else:
                if suite == "Allocation":
                    valid = (row.get("passed") is True and type(row.get("exitCode")) is int
                             and row["exitCode"] == 0 and row.get("timedOut") is not True
                             and report.get("passed") is True and report.get("measurementValid") is True
                             and report.get("objectLifecycleMeasurementValid") is True
                             and report.get("completedBatches", 0) > 0
                             and report.get("completedBatches") == report.get("measuredBatches")
                             and report.get("retiredDamageProbePassed") is True
                             and report.get("totalProductionRetired") == report.get("totalSuccessfulSpawnCalls"))
                    entry.update(status="measured" if valid else "invalid",
                                 byteMeasurementComplete=report.get("byteMeasurementComplete") is True,
                                 eventMeasurementComplete=report.get("eventMeasurementComplete") is True)
                records[suite] = entry
            if suite == "MapV2":
                entry["group"] = report.get("group", "all")
                for pattern in ("chapter-*.json", "area-comparison.json", "enemy-v2-*.csv"):
                    for path in sorted(report_path.parent.glob(pattern)):
                        add_same(evidence, prefix + path.relative_to(directory).as_posix(), path)
            if suite == "Models":
                frames = report.get("frames", [])
                require(isinstance(frames, list), "Models frames must be a list")
                entry["frames"] = len(frames)
                entry["frameEvidenceScope"] = "Declared native model frames; image files are retained through the separately supplied ui-review. No human approval inferred."
            if suite == "Tactical":
                # Preserve only the explicitly generated economy summaries, never fixture save files.
                for path in sorted(report_path.parent.rglob("economy-paths.*")):
                    if path.suffix in (".json", ".md"):
                        add(evidence, prefix + path.relative_to(directory).as_posix(), path)
    tactical = records.get("Tactical", {"status": "not_run"})
    route_ok = bool(route_records) and REQUIRED_ROUTES <= levels and all(r["status"] == "passed" for r in route_records)
    ui = {"status": "not_run", "scope": "No explicit UI review supplied"}
    windowed_sample = {"status": "not_run", "scope": "No supplementary windowed sample supplied"}
    if ui_review:
        review_path = ui_review.resolve(strict=True)
        review, raw = captured_json(review_path)
        matching_identity(review.get("buildIdentity", {}), identity, "UI review")
        require(review.get("status") in ("passed", "failed", "not_run"), "Unknown UI review status")
        if review["status"] == "passed":
            require(review.get("completed") is True and review.get("method") in ("native-offscreen", "foreground-agent", "native-windowed-script", "human")
                    and isinstance(review.get("files"), list) and bool(review["files"])
                    and review.get("reviewedUtc") and review.get("scope"), "Passed UI review requires actual evidence and scope")
        add(evidence, "ui-review/review.json", raw)
        for name in review.get("files", []):
            path = contained(review_path.parent, name)
            require(path.is_file() and path.stat().st_size > 0, "UI review attachment missing")
            if path.name.casefold() in ("foreground-performance.json", "performance-map-v2.json"):
                require(windowed_sample["status"] == "not_run", "Select only one final windowed performance sample")
                sample, sample_raw = captured_json(path)
                require(isinstance(sample, dict), "Windowed sample must contain an actual JSON object")
                windowed_sample = {
                    "status": "provided", "method": review.get("method"),
                    "evidence": "ui-review/" + name,
                    "reportSha256": hashlib.sha256(sample_raw).hexdigest(),
                    "scope": sample.get("scope", ""),
                    "sample": sample,
                    "scopeNote": "Supplied native windowed sample report; raw device/settings/focus fields and per-scene samples are retained as reported. Not the Performance suite, human playtesting, a before/after speedup, or a cross-device result.",
                }
                add(evidence, "ui-review/" + name, sample_raw)
            else:
                add(evidence, "ui-review/" + name, path)
        ui = {key: review.get(key) for key in ("status", "method", "scope", "reviewedUtc", "notes")}
        ui["scopeNote"] = (("Native MapV2 fixtures use the reported gameplay APIs, runtime camera renders and supplied window screenshots; the explicit review scope does not imply a human playthrough." if profile == "mapv2" else "Native windowed model fixtures use gameplay/UI review APIs and ScreenCapture; not human or OS-mouse interaction."
                            if profile == "models" else "Native windowed script uses InputSystem virtual keyboard and ScreenCapture; not human or OS-mouse interaction.")
                           if review.get("method") == "native-windowed-script"
                           else "Packager records the supplied review; it does not perform or certify human testing")
    status = {"profile": profile, "tactical": tactical,
              "mapv2": records.get("MapV2", {"status": "not_run"}),
              "models": records.get("Models", {"status": "not_run"}),
              "synergy": records.get("Synergy", {"status": "not_run"}),
              "storyd2": records.get("StoryD2", {"status": "not_run"}),
              "routes": {"status": "passed" if route_ok else "failed", "requiredLevels": sorted(REQUIRED_ROUTES),
                         "actualLevels": sorted(levels), "runs": route_records,
                         "scope": "清敌/耗尽跳板的保底路线夹具，非真人/真实战斗通关；不宣称全十二关重新验收"},
              "nativeVisual": records.get("Visual", {"status": "not_run"}),
              "uiReview": ui, "performance": records.get("Performance", {"status": "not_run"}),
              "windowedPerformanceSample": windowed_sample,
              "allocation": records.get("Allocation", {"status": "not_run"}),
              "cleanRebuild": {"status": "not_run_by_packager"},
              "humanPlaytesting": {"status": "not_certified_by_packager"}}
    if profile == "mapv2":
        require(status["mapv2"]["status"] == "passed", "MapV2 requires completed same-build MapV2 QA")
        require(status["mapv2"].get("group") == "all", "MapV2 delivery requires the complete all group, not a focused partial fixture")
        require(ui["status"] == "passed", "MapV2 requires supplied same-build actual UI review")
    elif profile == "storyd2":
        require(status["storyd2"]["status"] == "passed", "D2/N1 requires completed same-build StoryD2 QA")
        require(ui["status"] == "passed", "D2/N1 requires supplied same-build native UI evidence")
    elif profile == "synergy":
        require(status["synergy"]["status"] == "passed", "SSU-01 requires same-build Synergy QA")
        require(ui["status"] == "passed", "SSU-01 requires supplied native UI evidence")
    elif profile == "models":
        require(status["models"]["status"] == "passed", "Models packaging requires passed Models QA for this exact build")
    else:
        require(tactical["status"] == "passed", "D1.1 packaging requires passed Tactical full QA for this exact build")
    if profile not in ("synergy", "storyd2", "mapv2"):
        require(route_ok, "D1.1 packaging requires actual same-build routes 5, 7, 12; no skipped checkpoints")
    elif not route_records:
        status["routes"].update(status="not_run", requiredLevels=[], scope=("MapV2 geometry, player, enemy and equipment checks are recorded by the supplied MapV2 report; old D2/Synergy/Models/Routes are not substituted." if profile == "mapv2" else "D2/N1 changed chapter-5 geometry and rule boundaries are checked in StoryD2 QA; old Routes/Models/Synergy reports are not reused as new evidence." if profile == "storyd2" else "SSU-01 mission invariants are checked in Synergy QA; geometry unchanged, old route reports are not reused as new evidence."))
    return status, evidence


def licenses(project, source_payload):
    ledger_path = project / "THIRD_PARTY_ASSETS.json"
    ledger, raw = captured_json(ledger_path)
    entries = ledger.get("entries")
    require(isinstance(entries, list) and entries, "Actual third-party ledger entries are required")
    files = set(BASE_NOTICES) | {"THIRD_PARTY_ASSETS.md", "THIRD_PARTY_ASSETS.json"}
    accepted = []
    for entry in entries:
        name = entry.get("name", "")
        require(name and entry.get("license") and entry.get("sourceUrl"), "Ledger entry lacks provenance")
        require(entry.get("allowSource") is True and entry.get("allowPlayer") is True,
                "Both deliverables need explicit redistribution permission: " + name)
        paths, texts = entry.get("paths"), entry.get("licenseFiles")
        require(isinstance(paths, list) and paths and isinstance(texts, list) and texts, "Ledger entry has no real files/license: " + name)
        for relative in paths:
            path = contained(project, relative)
            require(path.exists(), "Adopted asset path is missing: " + relative)
            members = [path] if path.is_file() else sorted(p for p in path.rglob("*") if p.is_file())
            require(bool(members), "Adopted asset directory is empty: " + relative)
            for member in members:
                member_relative = member.relative_to(project).as_posix()
                if not private_file(member_relative):
                    require(member_relative in source_payload, "Ledger source was omitted/ignored: " + member_relative)
        license_text = ""
        for relative in texts:
            path = contained(project, relative)
            require(path.is_file() and path.stat().st_size > 0 and relative in source_payload, "Actual license text missing from source: " + relative)
            license_text += path.read_text(encoding="utf-8-sig").lower()
            files.add(relative)
        license_id = entry["license"].lower().replace(" ", "")
        if license_id.startswith("cc0"):
            require("cc0" in license_text or "creative commons zero" in license_text or "public domain dedication" in license_text,
                    "CC0 entry does not have its actual dedication/license text: " + name)
        if license_id == "mit":
            require("permission is hereby granted" in license_text, "MIT entry lacks the actual permission text: " + name)
        accepted.append({"name": name, "license": entry["license"], "sourceUrl": entry["sourceUrl"],
                         "paths": paths, "licenseFiles": texts})
    require(any(str(e["license"]).lower().startswith("cc0") for e in entries), "No adopted CC0 asset recorded")
    package_manifest = read_json(project / "Game/Packages/manifest.json")
    if "com.coplaydev.unity-mcp" in package_manifest.get("dependencies", {}):
        require(any("unity" in e["name"].lower() and "mcp" in e["name"].lower()
                    and e["license"].upper() == "MIT" for e in accepted), "Unity MCP dependency needs its explicit MIT notice/ledger entry")
    for relative in files:
        require(relative in source_payload, "License/notice missing from source selection: " + relative)
    return accepted, files, raw


def check_local_dependencies(project, source_payload):
    manifest = project / "Game/Packages/manifest.json"
    refs = []
    for name, value in read_json(manifest).get("dependencies", {}).items():
        if not isinstance(value, str) or not value.startswith("file:"):
            continue
        relative = value[5:]
        require(not Path(relative).is_absolute() and ":" not in relative, "Local package must not use an absolute machine path: " + name)
        folder = (manifest.parent / relative).resolve()
        require(folder.is_relative_to(project) and folder.is_dir(), "Local dependency must be present inside the public project: " + name)
        for path in folder.rglob("*"):
            if path.is_file():
                key = path.relative_to(project).as_posix()
                if not private_file(key):
                    require(key in source_payload, "Necessary local dependency was omitted: " + key)
        refs.append({"package": name, "path": folder.relative_to(project).as_posix()})
    return refs



def mcp_source_variant(project, windows_inputs, source_inputs):
    """One fixed dependency-source change, not a general build-equivalence policy."""
    commit = "aa5fc638d623f56178d50329d9ad8c541a57fe66"
    package_root = "Dependencies/com.coplaydev.unity-mcp"
    reference = "file:../../Dependencies/com.coplaydev.unity-mcp"
    original = {r["path"]: r["sha256"] for r in windows_inputs["files"]}
    revised = {r["path"]: r["sha256"] for r in source_inputs["files"]}
    removed = sorted(original.keys() - revised.keys())
    added = sorted(revised.keys() - original.keys())
    changed = sorted(name for name in original.keys() & revised.keys() if original[name] != revised[name])
    allowed = {"Game/Packages/manifest.json", "Game/Packages/packages-lock.json"}
    require(not removed and set(changed) <= allowed, "Source variant changed gameplay/assets/settings: " + ", ".join(removed + [p for p in changed if p not in allowed]))
    require(added and all(name.startswith("Dependencies/") for name in added)
            and "Dependencies" in source_inputs["scope"],
            "Source variant may only add explicitly hashed Dependencies inputs")
    game_inputs = {name: sha for name, sha in original.items()
                   if name.startswith("Game/Assets/") or name.startswith("Game/ProjectSettings/")}
    require(game_inputs == {name: sha for name, sha in revised.items()
                            if name.startswith("Game/Assets/") or name.startswith("Game/ProjectSettings/")},
            "Gameplay/assets/settings file sets or hashes differ")
    provenance = read_json(project / "Dependencies/unity-mcp-source.json")
    require(provenance.get("commit") == commit and provenance.get("version") == "10.3.0"
            and provenance.get("archiveSha256") == "d2353d0604e250184ee5c6fbb14b49d381a555f129977b09f80779b8c8df9b96",
            "Source variant requires the exact inspected official MCP commit/archive")
    files = provenance.get("upstreamFiles", [])
    canonical = "".join(item["path"] + "\t" + item["sha256"] + "\n" for item in files)
    require(len(files) == 744 and hashlib.sha256(canonical.encode("utf-8")).hexdigest()
            == "9d42eaefd603f2e572abbb35a0bc485d3d6ba96fad2c09ea12ebbd1c5c9d0626",
            "Pinned MCP upstream file manifest differs")
    for item in files:
        target = contained(project, package_root + "/" + item["path"])
        require(target.is_file() and digest(target) == item["sha256"], "Pinned MCP file changed: " + item["path"])
    actual = {p.relative_to(project / package_root).as_posix()
              for p in (project / package_root).rglob("*") if p.is_file()}
    require(actual == {item["path"] for item in files} | {"LICENSE", "LICENSE.meta"},
            "Unexpected files inside the pinned MCP package")
    package = read_json(project / package_root / "package.json")
    require(package.get("name") == "com.coplaydev.unity-mcp" and package.get("version") == "10.3.0",
            "MCP package name/version differs")
    previous_manifest = project / "Dependencies/McpProvenance/manifest.git-original.json"
    previous_lock = project / "Dependencies/McpProvenance/packages-lock.git-original.json"
    require(digest(previous_manifest) == original["Game/Packages/manifest.json"]
            and digest(previous_lock) == original["Game/Packages/packages-lock.json"],
            "Saved Git declarations do not match the accepted Windows build inputs")
    expected_manifest, expected_lock = read_json(previous_manifest), read_json(previous_lock)
    old_mcp = expected_lock["dependencies"]["com.coplaydev.unity-mcp"]
    require(old_mcp.get("source") == "git" and old_mcp.get("hash") == commit
            and old_mcp.get("dependencies") == package.get("dependencies"),
            "Accepted Windows dependency was not the same pinned MCP package")
    expected_manifest["dependencies"]["com.coplaydev.unity-mcp"] = reference
    old_mcp["version"] = reference
    old_mcp["source"] = "local"
    old_mcp.pop("hash")
    require(read_json(project / "Game/Packages/manifest.json") == expected_manifest
            and read_json(project / "Game/Packages/packages-lock.json") == expected_lock,
            "Package graph changed beyond replacing this MCP Git reference with the local path")
    return {"kind": "same-pinned-mcp-local-source", "gameplayAndAssetInputsIdentical": True,
            "matchingGameInputFiles": len(game_inputs), "changedBuildInputs": changed,
            "addedDependencyInputCount": len(added), "mcpVersion": "10.3.0", "mcpCommit": commit,
            "upstreamFileCount": 744,
            "scope": "Windows QA remains attached to its original accepted identity. Source uses its separately built identity; only this dependency location changed. No old report or source hash was rewritten."}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--executable", required=True, type=Path)
    parser.add_argument("--validation-dir", action="append", required=True, type=Path)
    parser.add_argument("--source-executable", type=Path,
                        help="Separately successful source build for the exact pinned-MCP local-source variant")
    parser.add_argument("--ui-review", type=Path)
    parser.add_argument("--profile", choices=("tactical", "models", "synergy", "storyd2", "mapv2"), default="tactical",
                        help="mapv2 requires completed same-build MapV2 + UI; storyd2 requires same-build StoryD2 + UI; synergy requires Synergy + UI; models requires Models + routes 5/7/12")
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--verification-root", type=Path)
    parser.add_argument("--check-only", action="store_true")
    args = parser.parse_args()
    project = args.project.resolve(strict=True)
    executable = args.executable.resolve(strict=True)
    output = args.output.resolve()
    verify_root = args.verification_root.resolve() if args.verification_root else output / "verification"
    require(not output.exists() and not verify_root.exists(), "Use new output/extraction directories; earlier deliveries are preserved")
    require(not output.is_relative_to(verify_root), "Extraction root cannot contain the delivery directory")
    for target in (output, verify_root):
        require(not target.is_relative_to(project) and not target.is_relative_to(executable.parent),
                "Delivery and extraction must be outside the project and original build")
    identity, build_source, assembly = identified_build(executable)
    validation, evidence = collect_evidence(args.validation_dir, identity, args.ui_review, args.profile)
    source_identity, source_inputs = identity, build_source
    source_variant = {"kind": "same-build", "gameplayAndAssetInputsIdentical": True}
    if args.source_executable:
        source_executable = args.source_executable.resolve(strict=True)
        for target in (output, verify_root):
            require(not target.is_relative_to(source_executable.parent), "Do not place delivery inside the source-build player")
        source_identity, source_inputs, _ = identified_build(source_executable)
        source_variant = mcp_source_variant(project, build_source, source_inputs)
    source_payload, source_info = collect_source(project, source_inputs)
    for name in list(source_payload):
        if private_file(name):
            del source_payload[name]
    require(set(SOURCE_TOOLS) <= source_payload.keys(), "New public tools/docs/ledger are missing or ignored: " + ", ".join(sorted(set(SOURCE_TOOLS)-source_payload.keys())))
    if args.profile == "mapv2":
        require("Tools/measure_map_v2.py" in source_payload, "MapV2 reproducible area tool is missing or ignored")
    for item in source_inputs["files"]:
        require(item["path"] in source_payload, "A build input is forbidden/omitted from source: " + item["path"])
    dependencies = check_local_dependencies(project, source_payload)
    ledger, license_files, ledger_bytes = licenses(project, source_payload)
    source_payload["THIRD_PARTY_ASSETS.json"] = ledger_bytes
    windows_payload = {}
    for path in sorted(executable.parent.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(executable.parent).as_posix()
        if private_file(relative) or any("DoNotShip" in part or "BackUpThisFolder_ButDontShip" in part for part in path.parts):
            continue
        require(path.resolve().is_relative_to(executable.parent) and not path.is_symlink(), "Player contains an external link")
        add(windows_payload, relative, path)
    windows_payload["build-identity.json"] = json_bytes(identity)
    windows_payload["source-manifest.json"] = json_bytes(build_source)
    require("-swapHunterTacticalDemo".encode("utf-16le") in assembly.read_bytes(),
            "This build does not contain the new independent four-class demonstration flag")
    launcher = """<# 手动启动独立四职业演示档；预置不代表真实通关，不写普通成长。 #>
param([ValidateRange(1,12)][int]$Level=5,[ValidateRange(0,3)][int]$Class=0,[ValidateRange(0,1)][int]$Skill=0,[switch]$Menu)
$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot '__EXE__'
$launchArguments=@('-swapHunterTacticalDemo','-demoLevel',$Level,'-demoClass',$Class,'-demoSkill',$Skill)
if($Menu){$launchArguments+='-demoMenu'}
& $exe @launchArguments
""".replace("__EXE__", executable.name.replace("'", "''"))
    add(windows_payload, "开始演示.ps1", b"\xef\xbb\xbf"+launcher.encode("utf-8"))
    require(executable.name == "SwapHunter.exe", "The double-click Windows launcher requires SwapHunter.exe")
    quick_launcher = b'@echo off\r\nstart "" "%~dp0SwapHunter.exe" -swapHunterTacticalDemo -demoLevel 5 -demoMenu\r\n'
    add(windows_payload, "快速体验.cmd", quick_launcher)
    for name in sorted(license_files):
        add_same(windows_payload, "Licenses/" + name, source_payload[name])
    add_same(windows_payload, "THIRD_PARTY_ASSETS.md", source_payload["THIRD_PARTY_ASSETS.md"])
    add_same(windows_payload, "THIRD_PARTY_ASSETS.json", ledger_bytes)
    selected_docs = (("docs/maps-enemies-v2-playguide.zh-CN.md", "docs/maps-enemies-v2-validation.zh-CN.md", "docs/map-v2-assets.zh-CN.md") if args.profile == "mapv2" else WINDOWS_DOCS + (("docs/homecoming-d2-playguide.zh-CN.md", "docs/homecoming-d2-validation.zh-CN.md") if args.profile == "storyd2" else ("docs/models-final.zh-CN.md", "docs/media/models-class.png", "docs/media/models-weapons.png", "docs/media/models-islands.png", "docs/media/models-actors.png") if args.profile == "models" else ("docs/system-synergy-playguide.zh-CN.md", "docs/system-synergy-validation.zh-CN.md") if args.profile == "synergy" else ()))
    for name in selected_docs:
        require(contained(project, name).is_file() and name in source_payload,
                "Final Chinese delivery document is missing or ignored: " + name)
        add_same(windows_payload, name, source_payload[name])
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    base = ("SwapHunter-MapV2-" if args.profile == "mapv2" else "SwapHunter-D2N1-" if args.profile == "storyd2" else "SwapHunter-SSU01-" if args.profile == "synergy" else "SwapHunter-ModelsFinal-" if args.profile == "models" else "SwapHunter-D11-") + stamp
    status = {"schemaVersion": 1, "releaseLabel": "MapV2 maps/enemies/player local candidate" if args.profile == "mapv2" else "D2/N1 homecoming local candidate" if args.profile == "storyd2" else "SSU-01 local candidate" if args.profile == "synergy" else "Models final" if args.profile == "models" else "D1.1",
              "profile": args.profile, "buildIdentity": identity,
              "validation": validation, "sourceSnapshot": source_info,
              "sourceBuildIdentity": source_identity, "sourceVariant": source_variant,
              "thirdPartyAssets": ledger, "localDependencies": dependencies,
              "remotePublication": "not_performed", "createdUtc": datetime.now(timezone.utc).isoformat()}
    template = (project / "docs/tactical-delivery-template.zh-CN.md").read_text(encoding="utf-8-sig")
    if args.profile == "models":
        template = template.replace("# 换位猎手 D1.1 本地交付", "# 换位猎手 Models 最终版 本地交付").replace("D1.1 是本轮系统方案标识。", "ModelsFinal 是本次全模型替换版本标识。")
        template = template.replace("- 同构建完整战术专项：{tactical_status}。",
            "- 同构建模型专项：{models_status}。\n- 战术经济专项记录：{tactical_status}；本模式不要求重跑经济，也不将旧构建报告冒充本轮结果。")
    if args.profile == "synergy":
        template = """# 换位猎手 SSU-01 系统协同升级 · 本地试玩版

游戏基础版本：{build_version}；规则版本 3；本候选未发布到远端。

- 同构建 SSU-01 专项：{synergy_status}。覆盖动作衔接、Boss 承诺、参数一致性、试用隔离、配装与行动记录。
- 新界面检查：{ui_status}。原生窗口脚本与截图，非真人通关。
- 未修改关卡几何；本轮检查相关任务约束，不重复执行旧模型和保底路线套件。

完整解压后按下方入口启动。升级操作见 `docs/system-synergy-playguide.zh-CN.md`。
源码导入 Unity Hub 时选择 Source 包中的 `Game`，编辑器 6000.3.25f1。

构建输入 SHA-256：`{source_sha256}`

程序集 SHA-256：`{assembly_sha256}`

源码基准：`{source_commit}`；含工作树修改：{dirty}。

`validation-status.json` 和交付目录 `evidence/` 保存实际验证范围。源码包不含玩家存档、Library、Git 和旧构建。
第三方材料的许可见 `THIRD_PARTY_ASSETS.md/json` 和 `Licenses/`。
"""
    if args.profile == "storyd2":
        template = """# 换位猎手 D2/N1 · 归航行动本地试玩版

游戏基础版本：{build_version}；D2 规则版本 4；本候选未发布到远端。

- 同构建剧情与系统联动专项：{storyd2_status}。
- 同构建新界面检查：{ui_status}。原生窗口脚本与截图。
- 专项覆盖本次 N1 剧情、D2 设备/中继/设施/伤害/AI/试用/Boss 和音乐资源；旧模型、旧路线和旧 Synergy 报告不作为本次新证据。

先阅读 `docs/homecoming-d2-playguide.zh-CN.md`，了解剧情按键、第5章联动玩法与两种启动入口。
验收范围与实际结果见 `docs/homecoming-d2-validation.zh-CN.md`、`validation-status.json` 及交付目录 `evidence/`。

N1 使用中文剧情卡、战斗无线电字幕与通讯回看。音频包含 3 首完整配乐映射 5 种状态、17 个选用音效及 1 段港口水岸环境声；无线电有接通提示音，无中文角色配音。

源码导入 Unity Hub 时选择 Source 包中的 `Game`，编辑器 6000.3.25f1。

构建输入 SHA-256：`{source_sha256}`

程序集 SHA-256：`{assembly_sha256}`

源码基准：`{source_commit}`；含工作树修改：{dirty}。

源码包不含玩家存档、Library、Git 和旧构建。第三方音乐、音效与模型的许可见 `THIRD_PARTY_ASSETS.md/json` 和 `Licenses/`。
本地脚本验收和画面检查不代表真人通关、平衡结论或性能提升。
"""
    if args.profile == "mapv2":
        status["rulesVersion"] = 5
        status["baseApplicationVersion"] = identity["version"]
        template = """# 换位猎手 MapV2 · 地图与敌人重构本地试玩版

游戏基础版本：{build_version}；规则版本 5；本候选未发布到远端。

- 同构建地图、敌人、操作与装备专项：{mapv2_status}。
- 同构建实际界面审阅：{ui_status}。
- 本版采用十二章独立地图、14种敌人原型、每章专属小Boss及保留的最终Boss。普通计划预算325，不是强制击杀数。
- 相位换位50米；跳板按新的跳跃输入触发、水平投送20米且可重复；六刀实际拾取并在成功结算后永久入库。

先阅读 `docs/maps-enemies-v2-playguide.zh-CN.md`。几何面积、专项范围及实际结果见 `docs/maps-enemies-v2-validation.zh-CN.md`、`validation-status.json` 与交付目录 `evidence/`。
快速体验使用独立全枪、六刀、技能演示档；正式成长仍由 `SwapHunter.exe` 进入普通v6档。

源码导入Unity Hub时选择Source包内的 `Game`，编辑器6000.3.25f1。

构建输入 SHA-256：`{source_sha256}`

程序集 SHA-256：`{assembly_sha256}`

源码基准：`{source_commit}`；含工作树修改：{dirty}。

本地专项与画面审阅不代表真人十二章通关、平衡结论或性能提升；192配装记录属于任务可达性快照核对，不是192次实战通关。
源码包不含玩家存档、Library、Git与旧构建。原创建模说明见 `docs/map-v2-assets.zh-CN.md`；既有第三方材料许可见 `THIRD_PARTY_ASSETS.md/json` 与 `Licenses/`。
"""
    readme = template.format(mapv2_status=validation["mapv2"]["status"], storyd2_status=validation["storyd2"]["status"], synergy_status=validation["synergy"]["status"], build_version=identity["version"], executable=executable.name,
        models_status=validation["models"]["status"],
        tactical_status=validation["tactical"]["status"], routes_status=validation["routes"]["status"],
        ui_status=validation["uiReview"]["status"], visual_status=validation["nativeVisual"]["status"],
        performance_status=validation["performance"]["status"], source_sha256=identity["sourceSha256"],
        assembly_sha256=identity["assemblySha256"], source_commit=source_info["baseGitCommit"],
        dirty=source_info["workingTreeDirty"])
    quick_start = (
        "- 快速体验：Windows包完整解压后双击 `快速体验.cmd`，直接打开独立四职业演示整备页，可浏览职业和全部章节；预置不代表真实通关。\n"
        "- 正常成长：双击 `SwapHunter.exe`，进入普通v6成长档。两种入口均保留完整玩家目录。\n"
        "- 带参数演示仍可用 `开始演示.ps1 -Level 5 -Class 0 -Skill 1 -Menu`；完整中文资料在 `docs/`，设计文档的源码链接需结合Source包阅读。"
    )
    readme = readme.replace("\n\n", "\n\n" + quick_start + "\n\n", 1)
    readme += ("\n附加窗口定点采样：" + validation["windowedPerformanceSample"]["status"]
               + "；原始设备、设置、聚焦和耗时字段见 validation-status.json 的 windowedPerformanceSample。它不替代传统Performance套件，也不证明性能提升或真人操作。\n")
    if args.source_executable:
        readme = readme.replace("构建输入 SHA-256：", "Windows已验收构建输入 SHA-256：")
        readme += ("\n源码包采用独立构建身份：输入SHA-256 " + source_identity["sourceSha256"]
                   + "；程序集SHA-256 " + source_identity["assemblySha256"]
                   + "。与Windows包相比，仅MCP由同commit Git引用改为随源码分发的本地UPM包；"
                   + "全部Game/Assets与ProjectSettings文件逐项哈希相同。Windows专项/UI/路线报告仍属于原Windows身份，未冒称新源码身份已重跑同一组游戏检查。\n")
    add(source_payload, "source-build-identity.json", json_bytes(source_identity))
    add(source_payload, "source-build-inputs.json", json_bytes(source_inputs))
    for payload in (windows_payload, source_payload):
        add(payload, "开始阅读.md", readme.encode("utf-8"))
        add(payload, "validation-status.json", json_bytes(status))
    windows_manifest = manifest_for(windows_payload, "windows", base+"-Windows", identity)
    verify_windows_manifest_identity(windows_manifest, identity, build_source, executable.name)
    source_manifest = manifest_for(source_payload, "source", base+"-Source", source_identity)
    source_records = {item["path"]: item for item in source_manifest["files"]}
    for item in source_inputs["files"]:
        require(source_records[item["path"]]["sha256"] == item["sha256"], "Build source changed during package preparation: "+item["path"])
    evidence_manifest = manifest_for(evidence, "source", "evidence", identity)
    if args.check_only:
        print(json.dumps({"checkOnly": True, "sourceFiles": len(source_payload), "windowsFiles": len(windows_payload),
                          "validation": validation, "licenses": ledger, "sourceVariant": source_variant,
                          "sourceBuildIdentity": source_identity}, ensure_ascii=False, indent=2))
        return 0
    output.mkdir(parents=True, exist_ok=False)
    (output/"manifests").mkdir()
    (output/"开始阅读.md").write_text(readme+f"\n本地文件：{base}-Windows.zip、{base}-Source.zip、SHA256SUMS.txt、package-report.json。\n", encoding="utf-8")
    (output/"validation-status.json").write_bytes(json_bytes(status))
    for item in evidence_manifest["files"]:
        name, value = item["path"], evidence[item["path"]]
        target = output/"evidence"/name
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("xb") as handle:
            if isinstance(value, bytes):
                handle.write(value)
            else:
                with value.open("rb") as source:
                    shutil.copyfileobj(source, handle)
        require(digest(target) == item["sha256"], "Evidence changed during copy; partial output preserved")
    (output/"evidence-files.json").write_bytes(json_bytes(evidence_manifest["files"]))
    verify_root.mkdir(parents=True, exist_ok=False)
    artifacts = []
    for kind, payload, manifest in (("windows",windows_payload,windows_manifest),("source",source_payload,source_manifest)):
        archive = output/(manifest["archiveRoot"]+".zip")
        manifest_path = output/"manifests"/(kind+"-manifest.json")
        manifest_path.write_bytes(json_bytes(manifest))
        write_archive(archive,payload,manifest)
        result = verify_archive(archive,verify_root/kind,manifest_path)
        artifacts.append({"kind":kind,"file":archive.name,"sha256":result["archiveSha256"],
                          "bytes":archive.stat().st_size,"files":result["fileCount"],"independentExtraction":result})
    status["artifacts"] = artifacts
    (output/"package-report.json").write_bytes(json_bytes(status))
    (output/"SHA256SUMS.txt").write_text("".join(item["sha256"]+"  "+item["file"]+"\n" for item in artifacts),encoding="utf-8")
    print(json.dumps({"artifacts":artifacts,"validation":validation},ensure_ascii=False,indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, json.JSONDecodeError) as error:
        print("D1.1 package rejected: "+str(error),file=sys.stderr)
        raise SystemExit(2)