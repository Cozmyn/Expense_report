"""Offline safety tests; do NOT claim this is a real infrastructure restore."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from argparse import Namespace
from unittest.mock import patch

from restore_drill import RecoveryError, read_manifest, require_isolated, run, verified_file


class RecoveryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory(prefix="decont-restore-test-")
        root = Path(self.tmp.name)
        self.dump = root / "backup.dump"
        self.dump.write_bytes(b"PGDMP" + b"example-only")
        self.keys = root / "keys"
        self.keys.mkdir()
        (self.keys / "key-11111111-2222-3333-4444-555555555555.xml").write_bytes(b"fake-test-key")
        self.objects = root / "objects"
        self.objects.mkdir()
        self.manifest = root / "manifest.json"
        self.manifest.write_text(json.dumps({
            "snapshot_utc": "2026-11-12T10:00:00Z",
            "database_sha256": hashlib.sha256(self.dump.read_bytes()).hexdigest(),
            "keys": {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                     for p in self.keys.iterdir()},
        }))
        self.args = Namespace(
            database_dump=str(self.dump), backup_objects=str(self.objects),
            backup_keys=str(self.keys), manifest=str(self.manifest), execute=False,
            database="", bucket="", confirm="", s3_endpoint="", target_keys="",
            incident_utc="", max_rpo_minutes=None, max_rto_seconds=None,
            verify_existing=False, restore_start_utc=None,
        )

    def tearDown(self) -> None:
        self.tmp.cleanup()

    def test_offline_integrity_check_never_claims_complete_restore(self) -> None:
        result = run(self.args)
        self.assertEqual("backup_integrity_only", result["status"])
        self.assertFalse(result["release_approved"])
        self.assertEqual(1, result["data_protection_keys"])

    def test_wrong_backup_hash_blocks_before_restore(self) -> None:
        self.dump.write_bytes(b"PGDMP" + b"tampered")
        with self.assertRaisesRegex(RecoveryError, "SHA-256"):
            read_manifest(self.manifest, self.dump, self.keys)

    def test_missing_or_changed_dataprotection_keys_fail_closed(self) -> None:
        for path in self.keys.iterdir():
            path.write_bytes(b"tampered key")
        with self.assertRaisesRegex(RecoveryError, "key backup"):
            read_manifest(self.manifest, self.dump, self.keys)

    def test_production_target_names_and_missing_confirmation_are_rejected(self) -> None:
        for db, bucket, confirmation in (
            ("decont", "decont-restore-test", "RESTORE_TO_ISOLATED_UAT_ONLY"),
            ("decont_restore_test", "decont-prod", "RESTORE_TO_ISOLATED_UAT_ONLY"),
            ("decont_restore_test", "decont-restore-test", ""),
        ):
            with self.assertRaises(RecoveryError):
                require_isolated(db, bucket, confirmation)
        require_isolated("decont_restore_test", "decont-restore-test",
                         "RESTORE_TO_ISOLATED_UAT_ONLY")

    def test_path_traversal_and_symlinks_are_rejected(self) -> None:
        for key in ("../prod", "/secrets", "a/../secret", "a//b", "a/./b"):
            with self.assertRaises(RecoveryError):
                verified_file(self.objects, key)
        (self.objects / "link").symlink_to(self.dump)
        with self.assertRaises(RecoveryError):
            verified_file(self.objects, "link")

    def test_requires_business_rpo_rto_before_external_io(self) -> None:
        self.args.execute = True
        self.args.database = "decont_restore_test"
        self.args.bucket = "decont-restore-test"
        self.args.confirm = "RESTORE_TO_ISOLATED_UAT_ONLY"
        with self.assertRaisesRegex(RecoveryError, "RPO and RTO"):
            run(self.args)

    def test_pitr_mode_cannot_be_claimed_by_offline_backup_inspection(self) -> None:
        self.args.verify_existing = True
        with self.assertRaisesRegex(RecoveryError, "requires --execute"):
            run(self.args)

    def test_rpo_breach_fails_without_running_pg_restore_or_s3(self) -> None:
        self.args.execute = True
        self.args.database = "decont_restore_test"
        self.args.bucket = "decont-restore-test"
        self.args.confirm = "RESTORE_TO_ISOLATED_UAT_ONLY"
        self.args.incident_utc = "2026-11-12T13:00:00Z"
        self.args.max_rpo_minutes = 5
        self.args.max_rto_seconds = 300
        with patch("restore_drill.command") as runner:
            with self.assertRaisesRegex(RecoveryError, "RPO"):
                run(self.args)
            runner.assert_not_called()


if __name__ == "__main__":
    unittest.main()
