import contextlib
import hashlib
import importlib.util
import io
import json
import pathlib
import tempfile
import unittest
import zipfile

SCRIPT = pathlib.Path(__file__).resolve().parents[1] / "archive-public-history.py"
spec = importlib.util.spec_from_file_location("public_history", SCRIPT)
archive = importlib.util.module_from_spec(spec)
spec.loader.exec_module(archive)


class PublicArchiveTests(unittest.TestCase):
    def snapshot(self, root, team=None):
        root.mkdir()
        (root / "blobs").mkdir()
        (root / "tables").mkdir()
        containers = []
        payloads = {"team": team if team is not None else {"name": "Test team"}}
        payloads.update({name: {"ClientSecret": "synthetic-excluded-secret"} for name in archive.EXCLUDED})
        for name, value in payloads.items():
            raw = json.dumps(value).encode()
            blob = {"name": "sample", "length": len(raw), "sha256": hashlib.sha256(raw).hexdigest()}
            archive.blob_file(root, name, "sample").write_bytes(raw)
            containers.append({"name": name, "blobs": [blob]})
        manifest = {"formatVersion": 1, "scope": "blob-table-current-state", "complete": True,
                    "failureCode": None, "completedAtUtc": "2026-11-01T00:00:00Z", "sourceQuiesced": True,
                    "containers": containers, "tables": [{"name": "SecurityOptions"}], "exclusions": []}
        (root / "manifest.json").write_text(json.dumps(manifest))
        (root / "tables" / "private.jsonl").write_text("synthetic-excluded-secret")
        (root / "database.dump").write_text("synthetic-excluded-secret")
        return (root / "manifest.json").read_bytes()

    def test_only_listed_allowed_blobs_enter_zip_and_originals_remain_intact(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            original = self.snapshot(root / "import")
            self.snapshot(root / "final")
            with contextlib.redirect_stdout(io.StringIO()):
                archive.build(root / "import", root / "final", root / "public")
            self.assertEqual(original, (root / "import" / "manifest.json").read_bytes())
            with zipfile.ZipFile(root / "public" / "2026-11-01.zip") as z:
                self.assertIsNone(z.testzip())
                for name in z.namelist():
                    self.assertNotIn("tables", pathlib.PurePosixPath(name).parts)
                    self.assertFalse(name.endswith(".dump"))
                    self.assertNotIn(b"synthetic-excluded-secret", z.read(name))
                for label in ["migration-import-snapshot", "legacy-final-snapshot"]:
                    manifest = json.loads(z.read(label + "/manifest.json"))
                    self.assertEqual([], manifest["tables"])
                    self.assertIn("Tables", manifest["exclusions"])
                    self.assertEqual(["team"], [c["name"] for c in manifest["containers"]])

    def test_nested_credentials_fail_without_disclosing_values(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            self.snapshot(root / "source", {"Value": json.dumps({"refresh_token": "synthetic-secret"})})
            with self.assertRaises(ValueError) as error:
                archive.filtered_snapshot(root / "source", root / "public")
            self.assertNotIn("synthetic-secret", str(error.exception))
            self.assertIn("Credential scan failed", str(error.exception))

    def test_private_rsa_components_are_detected(self):
        self.assertTrue(archive.credential_fields({"Rsa": {"Modulus": "synthetic", "D": "synthetic"}}))

    def test_existing_output_is_never_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            with self.assertRaises(FileExistsError):
                archive.build(root, root, root)


if __name__ == "__main__":
    unittest.main()
