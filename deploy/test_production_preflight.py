"""No-network tests for the read-only production configuration preflight."""

from __future__ import annotations

import contextlib
import io
import json
import unittest

from production_preflight import check, main


def plausible_configuration() -> dict[str, str]:
    # All values below are fake test data. Never use them for a real deployment.
    return {
        "ASPNETCORE_ENVIRONMENT": "Production",
        "Database__Provider": "Postgres",
        "ConnectionStrings__Database": (
            "Host=pg.example.invalid;Database=expense;Username=app;"
            "Password=unit-test-only-password;SSL Mode=VerifyFull;Trust Server Certificate=false"
        ),
        "Application__BaseUrl": "https://expense.example.invalid",
        "Storage__Provider": "S3",
        "Storage__Endpoint": "https://s3.example.invalid",
        "Storage__Bucket": "test-private-bucket",
        "Storage__AccessKey": "unit-test-only-access-key",
        "Storage__SecretKey": "unit-test-only-secret-key",
        "Scanner__Host": "scanner.internal",
        "Ocr__Enabled": "true",
        "Ocr__ServiceUrl": "http://ocr:8750",
        "Data__Directory": "/var/lib/expense/keys",
        "Smtp__Host": "smtp.example.invalid",
        "Smtp__From": "notify@example.invalid",
        "Session__Minutes": "30",
        "ReverseProxy__KnownProxies": "10.2.3.4",
    }


def blockers(env: dict[str, str]) -> set[str]:
    return {f.code for f in check(env) if f.severity == "blocker"}


class ProductionPreflightTests(unittest.TestCase):
    def test_missing_environment_is_blocked(self) -> None:
        codes = blockers({})
        self.assertIn("ENVIRONMENT", codes)
        self.assertIn("DATABASE_PROVIDER", codes)
        self.assertIn("PUBLIC_ORIGIN", codes)
        self.assertIn("PRIVATE_OBJECT_STORAGE", codes)
        self.assertIn("ANTIVIRUS_SCANNER", codes)

    def test_plausible_configuration_still_requires_manual_acceptance(self) -> None:
        findings = check(plausible_configuration())
        self.assertFalse([f for f in findings if f.severity == "blocker"])
        manual = {f.code for f in findings}
        self.assertIn("INGRESS_REVIEW", manual)
        self.assertIn("BACKUP_RESTORE", manual)
        self.assertIn("ERP_AND_OCR_ACCEPTANCE", manual)

    def test_unknown_or_missing_proxy_is_blocked(self) -> None:
        env = plausible_configuration()
        del env["ReverseProxy__KnownProxies"]
        self.assertIn("TRUSTED_PROXY", blockers(env))
        env["ReverseProxy__KnownProxies"] = "any"
        self.assertIn("TRUSTED_PROXY", blockers(env))
        env["ReverseProxy__KnownProxies"] = "10.2.3.4,10.2.3.5"
        self.assertNotIn("TRUSTED_PROXY", blockers(env))

    def test_local_development_compose_is_not_mistaken_for_production(self) -> None:
        env = plausible_configuration()
        env["ASPNETCORE_ENVIRONMENT"] = "Development"
        env["Application__BaseUrl"] = "http://localhost:5080"
        self.assertTrue({"ENVIRONMENT", "PUBLIC_ORIGIN"}.issubset(blockers(env)))

    def test_insecure_object_storage_endpoint_is_blocked(self) -> None:
        env = plausible_configuration()
        env["Storage__Endpoint"] = "http://s3.internal"
        self.assertIn("STORAGE_TLS", blockers(env))

    def test_database_certificate_bypass_is_blocked(self) -> None:
        env = plausible_configuration()
        env["ConnectionStrings__Database"] = env["ConnectionStrings__Database"].replace(
            "Trust Server Certificate=false", "Trust Server Certificate=true"
        )
        self.assertIn("DATABASE_TLS_BYPASS", blockers(env))

    def test_invalid_idle_timeout_is_blocked(self) -> None:
        env = plausible_configuration()
        env["Session__Minutes"] = "0"
        self.assertIn("SESSION_TIMEOUT", blockers(env))
        env["Session__Minutes"] = "unknown"
        self.assertIn("SESSION_TIMEOUT", blockers(env))

    def test_placeholder_credentials_are_blocked(self) -> None:
        env = plausible_configuration()
        env["Storage__SecretKey"] = "replace-with-s3-secret"
        self.assertIn("STORAGE_PLACEHOLDER", blockers(env))

    def test_public_origin_must_be_an_origin_not_a_link_with_credentials(self) -> None:
        env = plausible_configuration()
        env["Application__BaseUrl"] = "https://test:password@expense.example.invalid/recovery?x=1"
        self.assertIn("PUBLIC_ORIGIN", blockers(env))


    def test_paddleocr_is_mandatory_for_production_readiness(self) -> None:
        env = plausible_configuration()
        env.pop("Ocr__Enabled")
        env.pop("Ocr__ServiceUrl")
        self.assertTrue({"OCR_DISABLED", "OCR_SERVICE_URL"}.issubset(blockers(env)))
        env["Ocr__Enabled"] = "false"
        env["Ocr__ServiceUrl"] = "http://ocr:8750"
        self.assertIn("OCR_DISABLED", blockers(env))

    def test_ocr_service_is_never_a_public_or_localhost_url(self) -> None:
        env = plausible_configuration()
        for url in (
            "https://ocr.vendor.example.com/api",
            "http://127.0.0.1:8750",
            "http://localhost:8750",
            "http://0.0.0.0:8750",
            "http://someone:secret@ocr.internal:8750",
            "ftp://ocr.internal:8750",
            "http://ocr.internal:8750/?token=secret",
        ):
            env["Ocr__ServiceUrl"] = url
            self.assertIn("OCR_PRIVATE_ENDPOINT", blockers(env), url)

    def test_private_ocr_service_url_is_checked_but_not_certified(self) -> None:
        env = plausible_configuration()
        for url in ("http://ocr:8750", "http://ocr.internal:8750", "http://10.12.5.8:8750"):
            env["Ocr__ServiceUrl"] = url
            self.assertNotIn("OCR_PRIVATE_ENDPOINT", blockers(env), url)
            self.assertIn("OCR_SERVICE_VERIFICATION", {f.code for f in check(env)})

    def test_json_mode_never_prints_secret_values(self) -> None:
        env = plausible_configuration()
        env["Storage__SecretKey"] = "UNIQUE_SECRET_NEVER_OUTPUT_123"
        env["ConnectionStrings__Database"] += ";ApplicationName=UNIQUE_CONNECTION_NEVER_OUTPUT_456"
        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            self.assertEqual(0, main(["--json"], env))
        output = buffer.getvalue()
        self.assertNotIn("UNIQUE_SECRET_NEVER_OUTPUT_123", output)
        self.assertNotIn("UNIQUE_CONNECTION_NEVER_OUTPUT_456", output)
        result = json.loads(output)
        self.assertEqual("configuration_preflight_passed_only", result["result"])
        self.assertGreater(result["manual_reviews"], 0)


if __name__ == "__main__":
    unittest.main()
