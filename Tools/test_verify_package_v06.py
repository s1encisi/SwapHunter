"""Synthetic ZIP tests only: no real candidate package or external process."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import stat
import struct
import tempfile
import unittest
from unittest import mock
import warnings
import zipfile

import verify_package_v06 as verifier


class PackageVerifierTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="swaphunter-zip-test-")
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)
        self.archive = self.folder / "test.zip"
        self.destination = self.folder / "extracted"
        self.content = {"game.exe": b"synthetic exe", "data/notes.txt": b"synthetic data"}
        self.manifest = {
            "schemaVersion": 1, "artifact": "windows", "version": "0.6.0", "archiveRoot": "demo",
            "files": [{"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
                      for name, data in self.content.items()],
        }

    def make_zip(self, *, contents=None, extra=(), manifest_bytes=None, directories=()):
        contents = self.content if contents is None else contents
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            with zipfile.ZipFile(self.archive, "w", compression=zipfile.ZIP_STORED) as package:
                package.writestr("demo/package-manifest.json", manifest_bytes if manifest_bytes is not None
                                 else json.dumps(self.manifest).encode("utf-8"))
                for name, data in contents.items():
                    package.writestr(f"demo/{name}", data)
                for name in directories:
                    package.writestr(name, b"")
                for name, data in extra:
                    package.writestr(name, data)

    def reject(self):
        with self.assertRaises(verifier.PackageVerificationError):
            verifier.verify_archive(self.archive, self.destination)
        self.assertFalse(self.destination.exists())

    def test_valid_archive_and_independent_files(self):
        self.make_zip(directories=("demo/", "demo/data/"))
        external = self.folder / "manifest.json"
        external.write_text(json.dumps(self.manifest, indent=2), encoding="utf-8")
        result = verifier.verify_archive(self.archive, self.destination, external)
        self.assertTrue(result["passed"])
        self.assertEqual(result["fileCount"], 2)
        self.assertEqual(result["archiveSha256"], hashlib.sha256(self.archive.read_bytes()).hexdigest())
        self.assertEqual(Path(result["artifactRoot"]), self.destination / "demo")
        for name, data in self.content.items():
            self.assertEqual((self.destination / "demo" / name).read_bytes(), data)

    def test_missing_and_extra_files(self):
        for contents, extra in (({"game.exe": b"synthetic exe"}, ()),
                                (self.content, (("demo/extra.txt", b"extra"),))):
            with self.subTest(contents=contents, extra=extra):
                self.make_zip(contents=contents, extra=extra)
                self.reject()

    def test_duplicate_case_and_ancestor_conflicts(self):
        for entry in ("demo/game.exe", "demo/GAME.exe", "demo/Data/", "demo/game.exe/"):
            with self.subTest(entry=entry):
                self.make_zip(extra=((entry, b""),))
                self.reject()

    def test_unsafe_zip_names(self):
        for entry in ("/demo/escape", "../escape", "demo/../escape", "C:/escape", "demo/a:b",
                      "demo\\escape", "demo//escape", "demo/./escape", "demo/NUL.txt", "demo/a. "):
            with self.subTest(entry=entry):
                self.make_zip(extra=((entry, b"x"),))
                self.reject()

    def test_symlink_and_special_file(self):
        for mode in (stat.S_IFLNK | 0o777, stat.S_IFIFO | 0o644):
            info = zipfile.ZipInfo("demo/link")
            info.create_system = 3
            info.external_attr = mode << 16
            self.make_zip(extra=((info, b"../outside"),))
            self.reject()

    def test_unlisted_or_nonempty_directory(self):
        for extra in ((("demo/empty/", b""),), (("demo/data/", b"payload"),)):
            self.make_zip(extra=extra)
            self.reject()

    def test_wrong_sha_and_size(self):
        for field, value in (("bytes", 999), ("sha256", "0" * 64), ("bytes", True)):
            with self.subTest(field=field):
                item = self.manifest["files"][0]
                previous = item[field]
                item[field] = value
                self.make_zip()
                self.reject()
                item[field] = previous

    def test_crc_corruption(self):
        self.make_zip()
        data = self.archive.read_bytes()
        self.assertIn(b"synthetic exe", data)
        self.archive.write_bytes(data.replace(b"synthetic exe", b"Synthetic exe", 1))
        self.reject()

    def test_compressed_stream_failure_becomes_failed_report(self):
        compressed_content = b"compress me" * 20
        self.manifest["files"].append({"path": "compressed.bin", "bytes": len(compressed_content),
                                       "sha256": hashlib.sha256(compressed_content).hexdigest()})
        self.make_zip()
        with zipfile.ZipFile(self.archive, "a", compression=zipfile.ZIP_DEFLATED) as package:
            package.writestr("demo/compressed.bin", compressed_content)
        with zipfile.ZipFile(self.archive) as package:
            info = package.getinfo("demo/compressed.bin")
            offset = info.header_offset
        data = bytearray(self.archive.read_bytes())
        name_length, extra_length = struct.unpack_from("<HH", data, offset + 26)
        start = offset + 30 + name_length + extra_length
        # BFINAL=1 and reserved BTYPE=3 is invalid DEFLATE, independent of CRC.
        data[start] = 7
        self.archive.write_bytes(data)
        report = self.folder / "decompression-report.json"
        with contextlib.redirect_stdout(io.StringIO()):
            code = verifier.main(["--archive", str(self.archive), "--destination", str(self.destination),
                                  "--report", str(report)])
        self.assertEqual(code, 1)
        result = json.loads(report.read_text(encoding="utf-8"))
        self.assertFalse(result["passed"])
        self.assertIn("invalid block type", result["error"])
        self.assertFalse(self.destination.exists())

    def test_external_manifest_mismatch(self):
        self.make_zip()
        outside = self.folder / "outside.json"
        changed = dict(self.manifest, artifact="source")
        outside.write_text(json.dumps(changed), encoding="utf-8")
        with self.assertRaises(verifier.PackageVerificationError):
            verifier.verify_archive(self.archive, self.destination, outside)
        self.assertFalse(self.destination.exists())

    def test_external_manifest_json_types_must_match(self):
        self.manifest["extraMetadata"] = {"value": 1}
        self.make_zip()
        outside = self.folder / "outside.json"
        for field, value in (("schemaVersion", True), ("schemaVersion", 1.0),
                             ("extraMetadata", {"value": True}), ("extraMetadata", {"value": 1.0})):
            with self.subTest(field=field, value=value):
                changed = dict(self.manifest, **{field: value})
                outside.write_text(json.dumps(changed), encoding="utf-8")
                with self.assertRaises(verifier.PackageVerificationError):
                    verifier.verify_archive(self.archive, self.destination, outside)
                self.assertFalse(self.destination.exists())

    def test_manifest_schema_and_location(self):
        for field, value in (("schemaVersion", True), ("version", "0.5.2"), ("artifact", "other"),
                             ("archiveRoot", "other"), ("archiveRoot", "demo/sub")):
            with self.subTest(field=field, value=value):
                previous = self.manifest[field]
                self.manifest[field] = value
                self.make_zip()
                self.reject()
                self.manifest[field] = previous
        self.make_zip(extra=(("second/package-manifest.json", b"{}"),))
        self.reject()

    def test_manifest_paths_duplicate_ancestors_and_self(self):
        for path in ("game.exe", "GAME.exe", "game.exe/nested", "Data/other", "package-manifest.json", "../escape"):
            with self.subTest(path=path):
                self.manifest["files"].append({"path": path, "bytes": 0, "sha256": hashlib.sha256(b"").hexdigest()})
                self.make_zip()
                self.reject()
                self.manifest["files"].pop()

    def test_manifest_rejects_duplicate_json_keys(self):
        encoded = json.dumps(self.manifest).replace('"schemaVersion": 1', '"schemaVersion": 0, "schemaVersion": 1')
        self.make_zip(manifest_bytes=encoded.encode("utf-8"))
        self.reject()

    def test_existing_destination_is_untouched(self):
        self.make_zip()
        self.destination.mkdir()
        marker = self.destination / "keep.txt"
        marker.write_bytes(b"keep")
        with self.assertRaises(verifier.PackageVerificationError):
            verifier.verify_archive(self.archive, self.destination)
        self.assertEqual(marker.read_bytes(), b"keep")

    def test_extraction_is_rehashed_and_preserved_on_failure(self):
        self.make_zip()
        with mock.patch.object(verifier, "_digest_file", return_value=(0, "0" * 64)):
            with self.assertRaisesRegex(verifier.PackageVerificationError, "Independent extraction"):
                verifier.verify_archive(self.archive, self.destination)
        self.assertTrue((self.destination / "demo" / "game.exe").is_file())

    def test_cli_report_does_not_overwrite(self):
        self.make_zip()
        report = self.folder / "report.json"
        report.write_text("preserve", encoding="utf-8")
        with contextlib.redirect_stderr(io.StringIO()):
            code = verifier.main(["--archive", str(self.archive), "--destination", str(self.destination),
                                  "--report", str(report)])
        self.assertEqual(code, 1)
        self.assertEqual(report.read_text(encoding="utf-8"), "preserve")
        self.assertFalse(self.destination.exists())

    def test_cli_failure_report_is_truthful(self):
        self.make_zip(extra=(("demo/extra", b"extra"),))
        report = self.folder / "report.json"
        with contextlib.redirect_stdout(io.StringIO()):
            code = verifier.main(["--archive", str(self.archive), "--destination", str(self.destination),
                                  "--report", str(report)])
        self.assertEqual(code, 1)
        self.assertFalse(json.loads(report.read_text(encoding="utf-8"))["passed"])


if __name__ == "__main__":
    unittest.main()
