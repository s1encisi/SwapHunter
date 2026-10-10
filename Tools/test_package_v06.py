"""Synthetic gate tests only; no real player build, package or native QA is used."""
import json
import hashlib
from pathlib import Path
import tempfile
import unittest

from package_demo_v06 import collect_validation, excluded_source, expected_assets, route_full_coverage, json_bytes, manifest_for, verify_windows_manifest_identity


class PackageGates(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.identity = dict(version="0.6.0", status="Succeeded", sourceUnchanged=True,
                             sourceSha256="a" * 64, assemblySha256="b" * 64, executableSha256="c" * 64)

    def tearDown(self):
        self.temp.cleanup()

    @staticmethod
    def full_routes():
        return dict(version="0.6.0", passed=True, allTwelveCovered=True, selectedLevels=list(range(1, 13)),
                    checks=[dict(name="synthetic_gate_fixture", passed=True)],
                    chapters=[dict(level=level, passed=True) for level in range(1, 13)])

    def run_evidence(self, name, suites):
        from package_demo_v06 import EXPECTED_REPORTS
        root = self.root / name
        root.mkdir()
        def write(path, value):
            path.write_text(json.dumps(value), encoding="utf-8")
        write(root / "build-identity.json", self.identity)
        write(root / "assembly-sha256.json", {"Hash": self.identity["assemblySha256"]})
        write(root / "run-config.json", self.identity)
        rows = []
        for suite, report in suites.items():
            case = root / suite.lower()
            case.mkdir()
            if suite == "Visual":
                (case / "synthetic.png").write_bytes(b"synthetic presence fixture; not a native image")
            write(case / EXPECTED_REPORTS[suite], report)
            rows.append(dict(suite=suite, name=suite.lower(), report=case.name + "/" + EXPECTED_REPORTS[suite],
                             checks=len(report.get("checks", [])), passed=True, exitCode=0, timedOut=False,
                             displayMode="Background", sourceSha256=self.identity["sourceSha256"], assemblySha256=self.identity["assemblySha256"]))
        write(root / "summary.json", rows)
        return root

    def test_three_representatives_do_not_satisfy_routes(self):
        report = self.full_routes()
        report["selectedLevels"] = [2, 6, 7]
        report["chapters"] = [dict(level=level, passed=True) for level in (2, 6, 7)]
        self.assertFalse(route_full_coverage(report))

    def test_complete_distinct_twelve_routes_satisfy_gate(self):
        self.assertTrue(route_full_coverage(self.full_routes()))

    def test_twelve_rows_with_duplicate_chapter_do_not_pass(self):
        report = self.full_routes()
        report["chapters"][-1]["level"] = 11
        self.assertFalse(route_full_coverage(report))

    def test_failed_chapter_cannot_be_hidden_by_top_level_flag(self):
        report = self.full_routes()
        report["chapters"][4]["passed"] = False
        self.assertFalse(route_full_coverage(report))

    def test_boolean_chapter_does_not_count_as_chapter_one(self):
        report = self.full_routes()
        report["chapters"][0]["level"] = True
        self.assertFalse(route_full_coverage(report))

    def runtime_manifest(self):
        identity = dict(self.identity, executableSha256=hashlib.sha256(b"inert exe").hexdigest(),
                        assemblySha256=hashlib.sha256(b"inert assembly").hexdigest())
        source = {"files": [], "scope": []}
        payload = {"SwapHunter.exe": b"inert exe", "SwapHunter_Data/Managed/Assembly-CSharp.dll": b"inert assembly",
                   "build-identity.json": json_bytes(identity), "source-manifest.json": json_bytes(source)}
        return manifest_for(payload, "windows", "Test-Windows", identity), identity, source

    def test_manifest_identity_matches_accepted_snapshot(self):
        manifest, identity, source = self.runtime_manifest()
        verify_windows_manifest_identity(manifest, identity, source, "SwapHunter.exe")

    def test_replaced_assembly_cannot_inherit_old_qa_identity(self):
        manifest, identity, source = self.runtime_manifest()
        for record in manifest["files"]:
            if record["path"].endswith("Assembly-CSharp.dll"):
                record["sha256"] = "e" * 64
        with self.assertRaises(ValueError):
            verify_windows_manifest_identity(manifest, identity, source, "SwapHunter.exe")

    def test_replaced_identity_metadata_cannot_enter_accepted_package(self):
        manifest, identity, source = self.runtime_manifest()
        for record in manifest["files"]:
            if record["path"] == "source-manifest.json":
                record["sha256"] = "e" * 64
        with self.assertRaises(ValueError):
            verify_windows_manifest_identity(manifest, identity, source, "SwapHunter.exe")

    def test_old_or_other_build_identity_is_rejected(self):
        run = self.run_evidence("mismatch", {"Routes": self.full_routes()})
        altered = dict(self.identity, assemblySha256="d" * 64)
        (run / "build-identity.json").write_text(json.dumps(altered), encoding="utf-8")
        with self.assertRaises(ValueError):
            collect_validation([run], self.identity)

    def test_duplicate_suite_requires_explicit_final_selection(self):
        first = self.run_evidence("first", {"Routes": self.full_routes()})
        second = self.run_evidence("second", {"Routes": self.full_routes()})
        with self.assertRaises(ValueError):
            collect_validation([first, second], self.identity)

    def test_absent_suites_are_not_run_not_passed(self):
        run = self.run_evidence("partial", {"Routes": self.full_routes()})
        result, _ = collect_validation([run], self.identity)
        self.assertFalse(result["releaseGatePassed"])
        self.assertEqual(result["gates"]["Visual"]["status"], "not_run")
        self.assertEqual(result["performance"]["status"], "not_run")
        self.assertEqual(result["humanTesting"]["status"], "not_assessed")

    def test_all_six_program_gates_leave_human_and_performance_separate(self):
        report = dict(version="0.6.0", passed=True, checks=[dict(name="synthetic_gate_fixture", passed=True)])
        suites = {name: dict(report) for name in ("Core", "Missions", "Storage", "Gunplay")}
        suites["Routes"] = self.full_routes()
        suites["Visual"] = dict(report, frames=[dict(file="synthetic.png")], humanVisualReviewCompleted=False)
        run = self.run_evidence("complete", suites)
        result, _ = collect_validation([run], self.identity)
        self.assertTrue(result["releaseGatePassed"])
        self.assertEqual(result["performance"]["status"], "not_run")
        self.assertFalse(result["gates"]["Visual"]["humanVisualReviewCompleted"])

    def test_required_author_assets_have_exact_named_baseline(self):
        blends, models = expected_assets()
        self.assertEqual(len(blends), 17)
        self.assertEqual(len(models), 31)
        self.assertIn("Tools/BlenderAuthoring/blend/weapon-lineup.blend", blends)
        self.assertIn("Game/Assets/_SwapHunter/Art/ThreeModels/cx24-carbine.glb", models)

    def test_private_and_generated_materials_excluded(self):
        for name in (".git/config", "Game/Library/item", "Logs/report.json", "Builds/player.exe", "docs/interview-notes/private.md", "Game/AGENTS.md"):
            self.assertTrue(excluded_source(name), name)
        self.assertFalse(excluded_source("Game/README.md"))
        self.assertFalse(excluded_source("docs/architecture.zh-CN.md"))


if __name__ == "__main__":
    unittest.main()
