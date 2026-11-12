"""Offline tests for the Production UAT probe, never a Production certification."""
import unittest
from email.message import Message
from unittest.mock import Mock
from urllib.request import Request

from production_uat import AcceptanceError, HttpsOnly, cookie_flags, origin, resume, spoof_and_limit


class ProductionUatTests(unittest.TestCase):
    def test_https_origin_must_have_verified_host_without_credentials(self):
        for address in ("http://uat.example.invalid", "https://name:secret@uat.example.invalid",
                        "https://uat.example.invalid/path", "https://uat.example.invalid?x=1"):
            with self.assertRaises(AcceptanceError):
                origin(address)
        self.assertEqual("https://uat.example.invalid", origin("https://uat.example.invalid/"))

    def test_cookie_requires_secure_httponly_strict(self):
        good = Message()
        good.add_header("Set-Cookie", "decont.session=secret; path=/; Secure; HttpOnly; SameSite=Strict")
        cookie_flags(good, "decont.session")
        for flag in ("Secure", "HttpOnly", "SameSite=Strict"):
            bad = Message()
            bad.add_header("Set-Cookie", good["Set-Cookie"].replace(flag, ""))
            with self.assertRaises(AcceptanceError):
                cookie_flags(bad, "decont.session")

    def test_redirect_from_https_to_http_is_rejected(self):
        with self.assertRaises(AcceptanceError):
            HttpsOnly().redirect_request(Request("https://uat.example.invalid"), None,
                                          302, "Found", {}, "http://uat.example.invalid")

    def test_rate_limit_probe_cannot_target_normal_production_hostname(self):
        client = Mock()
        with self.assertRaises(AcceptanceError):
            spoof_and_limit(client, "expense.company.example")
        client.csrf.assert_not_called()

    def test_spoofed_forwarded_headers_must_not_bypass_limit(self):
        client = Mock()
        client.csrf.return_value = ("valid-csrf-token", Message())
        client.call.side_effect = [(401, Message(), b"{}")] * 15
        with self.assertRaisesRegex(AcceptanceError, "rate limits"):
            spoof_and_limit(client, "uat.expense.example")
        client.call.reset_mock(side_effect=True)
        client.call.side_effect = [(401, Message(), b"{}")] * 10 + [
            (429, Message(), b"{}"), (200, Message(), b"{}")]
        result = spoof_and_limit(client, "uat.expense.example")
        self.assertTrue(result["login_rate_limit_429"])

    def test_old_session_must_work_after_restart(self):
        client = Mock()
        client.call.side_effect = [(200, Message(), b"{}"), (401, Message(), b"{}")]
        with self.assertRaisesRegex(AcceptanceError, "Session lost"):
            resume(client)


if __name__ == "__main__":
    unittest.main()
