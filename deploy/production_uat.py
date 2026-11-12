#!/usr/bin/env python3
"""HTTPS Production-mode acceptance probes for an *isolated* UAT deployment.

Uses normal certificate and hostname verification, strict redirects, real cookie
and CSRF behavior. Never runs destructive finance flows: those require the
existing Playwright journeys and separate UAT signoff on seeded accounts.
"""
from __future__ import annotations

import argparse
import http.cookiejar
import json
import os
from pathlib import Path
import re
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request


class AcceptanceError(RuntimeError):
    pass


class HttpsOnly(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        if urllib.parse.urlsplit(newurl).scheme != "https":
            raise AcceptanceError("HTTPS ingress attempted an insecure redirect")
        return super().redirect_request(request, fp, code, msg, headers, newurl)


def origin(url: str) -> str:
    parsed = urllib.parse.urlsplit(url)
    if (parsed.scheme != "https" or not parsed.hostname or parsed.username or
        parsed.password or parsed.query or parsed.fragment or parsed.path not in ("", "/")):
        raise AcceptanceError("UAT must use an HTTPS origin without credentials or paths")
    if parsed.port and not 1 <= parsed.port <= 65535:
        raise AcceptanceError("Invalid HTTPS port")
    return url.rstrip("/")


class Client:
    def __init__(self, base: str, jar: http.cookiejar.MozillaCookieJar):
        self.base = origin(base)
        self.jar = jar
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPSHandler(context=ssl.create_default_context()),
            urllib.request.HTTPCookieProcessor(jar), HttpsOnly())

    def call(self, path: str, method="GET", payload=None, headers=None):
        data = json.dumps(payload).encode() if payload is not None else None
        req = urllib.request.Request(
            self.base + path, data=data, method=method,
            headers={"Accept": "application/json", **(
                {"Content-Type": "application/json"} if data is not None else {}),
                **(headers or {})})
        try:
            with self.opener.open(req, timeout=20) as response:
                return response.status, response.headers, response.read(2 * 1024 * 1024)
        except urllib.error.HTTPError as error:
            return error.code, error.headers, error.read(2 * 1024 * 1024)

    def csrf(self):
        status, headers, body = self.call("/api/auth/csrf")
        if status != 200:
            raise AcceptanceError("CSRF token endpoint did not return HTTP 200")
        token = json.loads(body)["token"]
        if not isinstance(token, str) or len(token) < 16:
            raise AcceptanceError("CSRF token is missing or too short")
        return token, headers


def cookie_flags(headers, prefix: str):
    matches = [cookie for cookie in headers.get_all("Set-Cookie", [])
               if cookie.lower().startswith(prefix.lower() + "=")]
    if len(matches) != 1:
        raise AcceptanceError("Expected a single " + prefix + " cookie")
    cookie = matches[0].lower()
    for flag in ("secure", "httponly", "samesite=strict"):
        if not re.search(r"(?:^|;)\s*" + re.escape(flag) + r"(?:;|$)", cookie):
            raise AcceptanceError("Cookie " + prefix + " is missing " + flag)


def initial(client: Client, username: str, password: str, jar_file: Path) -> dict:
    status, _, _ = client.call("/health/live")
    if status != 200:
        raise AcceptanceError("Health/live unavailable through trusted TLS ingress")
    status, _, _ = client.call("/health/ready")
    if status != 200:
        raise AcceptanceError("Database readiness unavailable through HTTPS")
    token, csrf_headers = client.csrf()
    # The antiforgery cookie is HttpOnly and Secure on production deployments.
    csrf_cookies = csrf_headers.get_all("Set-Cookie", [])
    if not csrf_cookies or any("secure" not in c.lower() or "httponly" not in c.lower()
                              for c in csrf_cookies):
        raise AcceptanceError("Insecure antiforgery cookie")
    status, login_headers, body = client.call(
        "/api/auth/login", "POST",
        {"username": username, "password": password},
        {"X-CSRF-TOKEN": token})
    if status != 200:
        raise AcceptanceError("Production HTTPS login failed")
    cookie_flags(login_headers, "decont.session")
    status, _, _ = client.call("/api/me")
    if status != 200:
        raise AcceptanceError("Session cannot reach authenticated /api/me")
    # Persist test-only HttpOnly cookies for the application restart probe.
    if jar_file.exists():
        raise AcceptanceError("Refusing to overwrite existing test session cookies")
    jar_file.parent.mkdir(parents=True, exist_ok=True)
    client.jar.save(str(jar_file), ignore_discard=True, ignore_expires=True)
    jar_file.chmod(0o600)
    return {"https_live": True, "database_ready": True, "csrf": True,
            "secure_session_cookie": True, "authenticated": True,
            "restart_session_cookie": str(jar_file)}


def resume(client: Client) -> dict:
    status, _, _ = client.call("/health/ready")
    if status != 200:
        raise AcceptanceError("Application failed readiness after restart/rollback")
    status, _, _ = client.call("/api/me")
    if status != 200:
        raise AcceptanceError("Session lost after restart; verify shared Data Protection keys")
    return {"restart_ready": True, "original_session_survived": True}


def spoof_and_limit(client: Client, hostname: str) -> dict:
    if not (re.search(r"(?:^|[.-])(uat|staging|test)(?:[.-]|$)", hostname, re.I)):
        raise AcceptanceError("Rate-limit test is prohibited outside an explicitly named UAT host")
    token, _ = client.csrf()
    seen_429 = False
    # Vary forged forwarded headers. A properly configured ingress MUST discard
    # them so that the application's per-client limiter still rejects requests.
    for index in range(15):
        status, _, _ = client.call(
            "/api/auth/login", "POST",
            {"username": "nonexistent-uat-probe-account", "password": "incorrect-test-only"},
            {"X-CSRF-TOKEN": token, "X-Forwarded-For": f"203.0.113.{index + 1}",
             "X-Forwarded-Proto": "http"})
        if status == 429:
            seen_429 = True
            break
        if status not in (400, 401):
            raise AcceptanceError("Unexpected HTTP code during ingress spoof test")
    if not seen_429:
        raise AcceptanceError("Spoofed X-Forwarded-For may bypass login rate limits")
    status, _, _ = client.call("/health/ready",
                               headers={"X-Forwarded-Proto": "http",
                                        "X-Forwarded-For": "203.0.113.200"})
    if status != 200:
        raise AcceptanceError("Spoofed forwarded scheme caused HTTPS redirect/availability failure")
    return {"forged_forwarded_headers_ignored": True, "login_rate_limit_429": True,
            "https_no_redirect_loop": True}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--mode", choices=["initial", "resume", "ingress"], required=True)
    parser.add_argument("--cookie-jar")
    parser.add_argument("--report", required=True)
    args = parser.parse_args(argv)
    result = {"status": "failed", "mode": args.mode, "release_approved": False}
    try:
        base = origin(args.base_url)
        parsed = urllib.parse.urlsplit(base)
        if args.mode in ("initial", "resume") and not args.cookie_jar:
            raise AcceptanceError("Initial/restart modes require a private cookie-jar path")
        jar = http.cookiejar.MozillaCookieJar()
        if args.mode == "resume":
            jar.load(args.cookie_jar, ignore_discard=True, ignore_expires=True)
        client = Client(base, jar)
        if args.mode == "initial":
            username = os.environ.get("UAT_USERNAME", "")
            password = os.environ.get("UAT_PASSWORD", "")
            if not username or not password:
                raise AcceptanceError("UAT_USERNAME and UAT_PASSWORD must be supplied via secrets")
            findings = initial(client, username, password, Path(args.cookie_jar))
        elif args.mode == "resume":
            findings = resume(client)
        else:
            findings = spoof_and_limit(client, parsed.hostname or "")
        result = {"status": "checks_passed_requires_full_workflow_uat", "mode": args.mode,
                  "checks": findings, "release_approved": False}
        code = 0
    except (AcceptanceError, OSError, urllib.error.URLError, ValueError, KeyError, json.JSONDecodeError) as error:
        result["message"] = (str(error) if isinstance(error, AcceptanceError)
                             else "HTTPS request or test data failed")
        code = 1
    path = Path(args.report)
    if path.exists():
        print("Refusing to overwrite UAT evidence", file=sys.stderr)
        return 1
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8") as stream:
        os.chmod(path, 0o600)
        json.dump(result, stream, ensure_ascii=False, indent=2)
    print("Production-mode UAT: " + result["status"] + "; evidence: " + str(path))
    return code


if __name__ == "__main__":
    sys.exit(main())
