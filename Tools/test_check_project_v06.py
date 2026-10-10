"""Negative fixtures for the cheap source-closure gate; no game/Unity execution."""
import json
from pathlib import Path
import struct
import tempfile
import unittest

from check_project_v06 import Checks, check_imports, check_links, check_guids, glb_nodes


class SourceContracts(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def write(self, name, content):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        return path

    def test_missing_relative_module_is_rejected(self):
        self.write("Tools/ArtAuthoring/generate.mjs", "import { gun } from './missing.mjs';")
        checks = Checks()
        check_imports(self.root, checks)
        self.assertFalse(checks.passed)
        self.assertIn("missing.mjs", checks.results[0]["name"])

    def test_supplied_relative_module_passes(self):
        self.write("Tools/ArtAuthoring/generate.mjs", "import { gun } from './gun.mjs';")
        self.write("Tools/ArtAuthoring/gun.mjs", "export const gun = 1;")
        checks = Checks()
        check_imports(self.root, checks)
        self.assertTrue(checks.passed)

    def test_deleted_link_target_is_rejected(self):
        self.write("README.md", "[验收](docs/not-shipped.md)")
        checks = Checks()
        check_links(self.root, checks)
        self.assertFalse(checks.passed)

    def test_spaces_and_code_examples_do_not_create_false_link_failures(self):
        self.write("README.md", "[doc](<docs/two words.md#section>)\n```md\n[example](absent.md)\n```")
        self.write("docs/two words.md", "# section")
        checks = Checks()
        check_links(self.root, checks)
        self.assertTrue(checks.passed)

    def test_duplicate_guid_is_rejected(self):
        for name in ("One", "Two"):
            self.write(f"Game/Assets/_SwapHunter/{name}.cs", "// fixture")
            self.write(f"Game/Assets/_SwapHunter/{name}.cs.meta", "guid: " + "a" * 32 + "\n")
        checks = Checks()
        check_guids(self.root, checks)
        self.assertFalse(checks.passed)
        self.assertTrue(any(not c["passed"] and c["name"].startswith("unique_guid:") for c in checks.results))

    def test_new_unpaired_unity_script_is_rejected(self):
        self.write("Game/Assets/_SwapHunter/NewRule.cs", "// fixture")
        checks = Checks()
        check_guids(self.root, checks)
        self.assertFalse(checks.passed)

    def test_glb_declared_size_mismatch_is_rejected(self):
        data = json.dumps({"nodes": [{"name": "Muzzle"}]}).encode()
        path = self.root / "model.glb"
        path.write_bytes(struct.pack("<IIIII", 0x46546C67, 2, 9999, len(data), 0x4E4F534A) + data)
        with self.assertRaises(ValueError):
            glb_nodes(path)

    def test_glb_node_names_are_read_from_binary(self):
        data = json.dumps({"nodes": [{"name": "Muzzle"}, {"name": "magazine"}]}).encode()
        path = self.root / "model.glb"
        path.write_bytes(struct.pack("<IIIII", 0x46546C67, 2, 20 + len(data), len(data), 0x4E4F534A) + data)
        self.assertEqual(glb_nodes(path), {"Muzzle", "magazine"})


if __name__ == "__main__":
    unittest.main()
