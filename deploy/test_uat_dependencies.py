"""Offline fail-closed tests for Production-mode service probes."""
import json
import os
import unittest
from unittest.mock import patch

from uat_dependencies import (DependencyError, EICAR, isolated_host,
                               postgres_verified_tls, s3_private_versioned,
                               scanner_clean_and_infected, ocr_engine_ready)


class DependencyProbesTests(unittest.TestCase):
    def test_only_isolated_uat_hosts(self):
        self.assertTrue(isolated_host("https://uat.expense.example.invalid"))
        self.assertTrue(isolated_host("https://expense-staging.example.invalid"))
        self.assertFalse(isolated_host("https://expense.example.com"))

    def test_postgres_must_use_validated_tls(self):
        with patch.dict(os.environ, {"PGHOST": "pg-uat", "PGSSLMODE": "require",
                                     "PGSSLROOTCERT": "/fake/ca.pem"}):
            with self.assertRaisesRegex(DependencyError, "verify-full"):
                postgres_verified_tls()
        with patch.dict(os.environ, {"PGHOST": "pg-uat", "PGSSLMODE": "verify-full",
                                     "PGSSLROOTCERT": "/fake/ca.pem"}), patch(
                                        "uat_dependencies.subprocess_text", return_value="t|TLSv1.3|TLS_AES_256_GCM_SHA384"):
            report = postgres_verified_tls()
            self.assertTrue(report["tls"])

    def test_s3_rejects_public_acl_disabled_block_and_missing_versioning(self):
        payloads = {
            "get-public-access-block": {"PublicAccessBlockConfiguration": {
                "BlockPublicAcls": True, "IgnorePublicAcls": True,
                "BlockPublicPolicy": True, "RestrictPublicBuckets": True,
            }},
            "get-bucket-policy-status": {"PolicyStatus": {"IsPublic": False}},
            "get-bucket-acl": {"Grants": []},
            "get-bucket-versioning": {"Status": "Enabled"},
            "get-bucket-encryption": {"ServerSideEncryptionConfiguration": {"Rules": [{}]}},
        }
        def fake_aws(command, env=None):
            return json.dumps(payloads[command[4]])
        with patch.dict(os.environ, {"UAT_S3_ENDPOINT": "https://s3.uat.example.invalid",
                                     "UAT_S3_BUCKET": "decont-uat-e2etest"}), patch(
                                         "uat_dependencies.subprocess_text", side_effect=fake_aws):
            self.assertTrue(s3_private_versioned()["private"])
            payloads["get-bucket-policy-status"]["PolicyStatus"]["IsPublic"] = True
            with self.assertRaisesRegex(DependencyError, "Private"):
                s3_private_versioned()
            payloads["get-bucket-policy-status"]["PolicyStatus"]["IsPublic"] = False
            payloads["get-bucket-versioning"]["Status"] = "Suspended"
            with self.assertRaisesRegex(DependencyError, "versioning"):
                s3_private_versioned()

    def test_scanner_requires_clean_and_virus_signature(self):
        with patch.dict(os.environ, {"UAT_SCANNER_HOST": "clamav.uat.local"}), patch(
            "uat_dependencies.clamav_stream",
            side_effect=lambda _h, _p, payload:
                "stream: Eicar-Test-Signature FOUND" if payload == EICAR else "stream: OK"
        ):
            result = scanner_clean_and_infected()
            self.assertTrue(result["eicar_blocked"])
        with patch.dict(os.environ, {"UAT_SCANNER_HOST": "clamav.uat.local"}), patch(
            "uat_dependencies.clamav_stream", return_value="stream: OK"
        ):
            with self.assertRaisesRegex(DependencyError, "ClamAV"):
                scanner_clean_and_infected()

    def test_ocr_requires_real_ready_status(self):
        from io import BytesIO
        with patch.dict(os.environ, {"UAT_OCR_URL": "http://ocr.uat.internal:8750"}), patch(
            "uat_dependencies.urlopen", return_value=BytesIO(b'{"status":"Ready","engine":"PP-OCRv6"}')
        ):
            self.assertTrue(ocr_engine_ready()["ready"])
        with patch.dict(os.environ, {"UAT_OCR_URL": "http://ocr.uat.internal:8750"}), patch(
            "uat_dependencies.urlopen", return_value=BytesIO(b'{"status":"Unavailable"}')
        ):
            with self.assertRaisesRegex(DependencyError, "not ready"):
                ocr_engine_ready()


if __name__ == "__main__":
    unittest.main()
