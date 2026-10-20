"""Read-only source/asset contract checks. Python standard library; no Unity licence.

This validates source closure and configuration, not compilation or playability.
Only --report writes a new JSON file; existing reports are never replaced.
"""
import argparse
import json
from pathlib import Path
import re
import struct
import subprocess
import sys
from urllib.parse import unquote

VERSION = "0.7.0"
EDITOR = "6000.3.25f1"
REQUIRED = (
    "Tools/ArtAuthoring/generate-assets.mjs",
    "Tools/ArtAuthoring/tactical-weapons-v052.mjs",
    "Tools/ArtAuthoring/generate-base-weapons-v052.mjs",
    "Tools/ArtAuthoring/base-weapons-v052-manifest.json",
    "Tools/BlenderAuthoring/generate_v04.py",
    "Tools/BlenderAuthoring/generate_combat_v05.py",
    "Game/Assets/_SwapHunter/Scenes/SwapHunter.unity",
    "Game/Assets/_SwapHunter/Data/DemoConfig.asset",
    "Game/Assets/_SwapHunter/Data/ModelLibrary.asset",
    "Game/Assets/_SwapHunter/Resources/MissionConfiguration.asset",
)


class Checks:
    def __init__(self):
        self.results = []

    def add(self, name, passed, detail=""):
        self.results.append(dict(name=name, passed=bool(passed), detail=str(detail)))

    @property
    def passed(self):
        return bool(self.results) and all(c["passed"] for c in self.results)


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def check_imports(root, checks):
    for file in sorted((root / "Tools/ArtAuthoring").glob("*.mjs")):
        source = file.read_text(encoding="utf-8-sig")
        imports = re.findall(r"(?:from\s*|import\s*\(?\s*)['\"](\.[^'\"]+)['\"]", source)
        for target in imports:
            checks.add("js_import:" + str(file.relative_to(root)) + ":" + target,
                       (file.parent / target).is_file(), "Local authoring dependency must be shipped")


def local_links(source):
    # Ignore code examples. Support ordinary and <space-containing> Markdown targets.
    source = re.sub(r"```.*?```", "", source, flags=re.S)
    return re.findall(r"!?\[[^\]]*\]\(\s*(<[^>]+>|[^\s)]+)(?:\s+[\"'][^)]*)?\s*\)", source)


def check_links(root, checks):
    documents = [root / "README.md", root / "Game/README.md"]
    documents += list((root / "docs").rglob("*.md"))
    documents += list((root / "Tools").glob("*/README.md"))
    for file in sorted(set(documents)):
        if not file.is_file():
            continue
        for target in local_links(file.read_text(encoding="utf-8-sig")):
            target = unquote(target.strip("<>").split("#", 1)[0].split("?", 1)[0])
            if not target or re.match(r"[a-zA-Z][a-zA-Z0-9+.-]*:", target) or target.startswith("//"):
                continue
            destination = (root / target.lstrip("/")) if target.startswith("/") else file.parent / target
            checks.add("local_link:" + str(file.relative_to(root)) + ":" + target,
                       destination.exists(), "Referenced local document or media must exist")


def check_guids(root, checks):
    assets = root / "Game/Assets"
    guids = {}
    for file in sorted(assets.rglob("*.meta")):
        match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", file.read_text(encoding="utf-8-sig"), re.M)
        checks.add("meta_guid:" + str(file.relative_to(root)), match is not None)
        if match:
            guid = match.group(1)
            checks.add("unique_guid:" + str(file.relative_to(root)), guid not in guids,
                       guids.get(guid, ""))
            guids[guid] = str(file.relative_to(root))
    extensions = {".cs", ".glb", ".unity", ".asset", ".mat", ".shader", ".otf"}
    for file in sorted((assets / "_SwapHunter").rglob("*")):
        if file.is_file() and file.suffix in extensions:
            checks.add("asset_meta:" + str(file.relative_to(root)), Path(str(file) + ".meta").is_file())


def glb_nodes(path):
    with path.open("rb") as handle:
        header = handle.read(20)
        magic, version, length, json_length, json_kind = struct.unpack("<IIIII", header)
        if magic != 0x46546C67 or version != 2 or length != path.stat().st_size or json_kind != 0x4E4F534A:
            raise ValueError("Invalid GLB header/length/JSON chunk")
        data = json.loads(handle.read(json_length))
    return {node.get("name") for node in data.get("nodes", [])}


def check_base_models(root, checks):
    for name in ("cx24-carbine", "s12-scattergun"):
        path = root / "Game/Assets/_SwapHunter/Art/ThreeModels" / (name + ".glb")
        try:
            missing = {"Muzzle", "AimRear", "AimFront", "magazine", "charging_handle"} - glb_nodes(path)
            checks.add("base_model_nodes:" + name, not missing, ", ".join(sorted(missing)))
        except (OSError, ValueError, struct.error) as error:
            checks.add("base_model_nodes:" + name, False, error)


def check_configuration(root, checks):
    settings = (root / "Game/ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8-sig")
    checks.add("player_version", re.search(r"^\s*bundleVersion:\s*" + re.escape(VERSION) + r"\s*$", settings, re.M))
    editor = (root / "Game/ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig")
    checks.add("unity_version", "m_EditorVersion: " + EDITOR in editor)
    manifest = read_json(root / "Game/Packages/manifest.json")["dependencies"]
    locked = read_json(root / "Game/Packages/packages-lock.json")["dependencies"]
    for package, version in manifest.items():
        checks.add("unity_package_lock:" + package, locked.get(package, {}).get("version") == version)
    node_manifest = read_json(root / "Tools/ArtAuthoring/package.json")
    node_locked = read_json(root / "Tools/ArtAuthoring/package-lock.json")["packages"]
    for package, version in node_manifest["dependencies"].items():
        checks.add("npm_package_lock:" + package, node_locked.get("node_modules/" + package, {}).get("version") == version)
    build_settings = (root / "Game/ProjectSettings/EditorBuildSettings.asset").read_text(encoding="utf-8-sig")
    scenes = re.findall(r"enabled:\s*1\s+path:\s*(.+)", build_settings)
    checks.add("entry_scene_enabled", "Assets/_SwapHunter/Scenes/SwapHunter.unity" in scenes)
    for scene in scenes:
        checks.add("build_scene:" + scene, (root / "Game" / scene).is_file())
    build_source = (root / "Game/Assets/_SwapHunter/Scripts/Editor/BuildDemo.cs").read_text(encoding="utf-8-sig")
    checks.add("build_version", 'Version = "' + VERSION + '"' in build_source)


def check_project(root):
    checks = Checks()
    for name in REQUIRED:
        checks.add("required_file:" + name, (root / name).is_file())
    # In a source ZIP no Git metadata is required; in a checkout detect ignored author dependencies.
    if (root / ".git").exists():
        result = subprocess.run(["git", "-C", str(root), "check-ignore", "--", *REQUIRED], capture_output=True, text=True)
        checks.add("author_files_not_ignored", result.returncode == 1, result.stdout.strip() or result.stderr.strip())
    for name, operation in (("imports", check_imports), ("links", check_links),
                            ("guids", check_guids), ("base_models", check_base_models),
                            ("configuration", check_configuration)):
        try:
            operation(root, checks)
        except (OSError, ValueError, KeyError) as error:
            checks.add(name, False, error)
    return checks


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--report", type=Path, help="Write a new report; never overwrite existing evidence")
    args = parser.parse_args()
    checks = check_project(args.root.resolve())
    report = dict(version=VERSION, passed=checks.passed,
                  scope="Static source closure, local links, asset GUID/node and package/config checks; no Unity compilation or player run",
                  checks=checks.results)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        with args.report.open("x", encoding="utf-8") as handle:
            json.dump(report, handle, ensure_ascii=False, indent=2)
    for check in checks.results:
        if not check["passed"]:
            print("FAIL " + check["name"] + " " + check["detail"])
    print(f"Static checks: {len(checks.results)}; passed={checks.passed}")
    return 0 if checks.passed else 1


if __name__ == "__main__":
    sys.exit(main())
