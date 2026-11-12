"""Offline, synthetic proof that P0 release gate cannot be bypassed by green CI."""
import json
from pathlib import Path
import tempfile
import unittest

from release_gate import GateError, REQUIRED_SCENARIOS, evaluate


class ReleaseGateTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.path = Path(self.tmp.name)
        self.digest = "sha256:" + "a" * 64
        self.write("restore.json", {
            "status": "integrity_verified_requires_application_uat",
            "objects_verified": 3, "exports_verified": 1,
            "data_protection_keys_verified": 2,
            "duration_seconds": 42, "rpo_minutes": 1,
        })
        for mode, filename in (("initial", "uat-initial.json"),
                               ("resume", "uat-resume.json"), ("ingress", "uat-ingress.json")):
            self.write(filename, {
                "status": "checks_passed_requires_full_workflow_uat", "mode": mode,
            })
        self.flows = {
            "built_image_digest": self.digest,
            "tested_image_digest": self.digest,
            "promoted_image_digest": self.digest,
            "scenarios": {name: {"status": "passed", "artifact": "https://uat.test/evidence/" + name}
                          for name in REQUIRED_SCENARIOS},
        }
        self.write("workflows.json", self.flows)

    def tearDown(self):
        self.tmp.cleanup()

    def write(self, filename, value):
        (self.path / filename).write_text(json.dumps(value))

    def test_complete_packet_is_not_itself_production_approval(self):
        result = evaluate(self.path)
        self.assertEqual(len(REQUIRED_SCENARIOS), result["tested_scenarios"])
        self.assertFalse(result["release_approved"])

    def test_missing_restore_or_rollback_blocks_release(self):
        (self.path / "restore.json").unlink()
        with self.assertRaises(GateError):
            evaluate(self.path)
        self.write("restore.json", {"status": "backup_integrity_only"})
        with self.assertRaises(GateError):
            evaluate(self.path)
        self.write("restore.json", {
            "status": "integrity_verified_requires_application_uat",
            "objects_verified": 3, "exports_verified": 1,
            "data_protection_keys_verified": 2,
        })
        del self.flows["scenarios"]["rollback_old_image"]
        self.write("workflows.json", self.flows)
        with self.assertRaisesRegex(GateError, "rollback_old_image"):
            evaluate(self.path)

    def test_image_rebuild_or_different_promotion_digest_blocks(self):
        self.flows["promoted_image_digest"] = "sha256:" + "b" * 64
        self.write("workflows.json", self.flows)
        with self.assertRaisesRegex(GateError, "identical OCI"):
            evaluate(self.path)

    def test_status_without_linked_artifact_is_not_evidence(self):
        self.flows["scenarios"]["smtp_delivery"]["artifact"] = ""
        self.write("workflows.json", self.flows)
        with self.assertRaisesRegex(GateError, "smtp_delivery"):
            evaluate(self.path)


if __name__ == "__main__":
    unittest.main()
