#!/usr/bin/env python3
"""Real read-only/isolated UAT dependency probes for OPS-01/OPS-02.

Requires an isolated HTTPS UAT host. No production sources or files are written.
The scanner receives the harmless industry-standard EICAR antivirus test string
in memory. SMTP sends a single labelled test email only with explicit opt-in.
The result is a redacted report, never a Production approval.
"""
from __future__ import annotations

import argparse
import json
import os
from email.message import EmailMessage
from pathlib import Path
import re
import smtplib
import socket
import ssl
import struct
import subprocess
import sys
from urllib.parse import urlsplit
from urllib.request import urlopen, Request


EICAR = (
    b"X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"
)


class DependencyError(RuntimeError):
    pass


def isolated_host(value: str) -> bool:
    host = urlsplit(value).hostname or ""
    return bool(re.search(r"(?:^|[.-])(uat|staging|test)(?:[.-]|$)", host, re.I))


def subprocess_text(command: list[str], env: dict | None = None) -> str:
    try:
        p = subprocess.run(command, env=env, check=True, capture_output=True, text=True, timeout=35)
        return p.stdout
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as exc:
        raise DependencyError("Dependency command failed: " + command[0]) from exc


def postgres_verified_tls() -> dict:
    if not os.environ.get("PGHOST") or os.environ.get("PGSSLMODE", "").lower() != "verify-full":
        raise DependencyError("PostgreSQL requires PGHOST and PGSSLMODE=verify-full")
    if not os.environ.get("PGSSLROOTCERT"):
        raise DependencyError("UAT PostgreSQL requires a verified CA root certificate file")
    query = ("SELECT ssl::text || '|' || COALESCE(version,'') || '|' || COALESCE(cipher,'') "
             "FROM pg_stat_ssl WHERE pid = pg_backend_pid();")
    response = subprocess_text(["psql", "-X", "-At", "-v", "ON_ERROR_STOP=1", "-c", query])
    fields = response.strip().split("|")
    if len(fields) != 3 or fields[0].lower() not in ("t", "true") or not fields[1].startswith("TLS"):
        raise DependencyError("Database session is not using verified TLS")
    return {"tls": True, "protocol": fields[1], "cipher_present": bool(fields[2])}


def s3_private_versioned() -> dict:
    endpoint = os.environ.get("UAT_S3_ENDPOINT", "")
    bucket = os.environ.get("UAT_S3_BUCKET", "")
    if not (endpoint.startswith("https://") and urlsplit(endpoint).hostname and bucket):
        raise DependencyError("UAT S3 requires explicit HTTPS endpoint and private bucket")
    if not re.fullmatch(r"decont-(?:uat|test|staging)-[a-z0-9-]{4,55}", bucket):
        raise DependencyError("S3 acceptance may use only isolated UAT/test buckets")

    def aws(op: str) -> dict:
        response = subprocess_text(["aws", "--endpoint-url", endpoint, "s3api",
                                   op, "--bucket", bucket, "--output", "json"])
        return json.loads(response)

    block = aws("get-public-access-block").get("PublicAccessBlockConfiguration", {})
    fields = ("BlockPublicAcls", "IgnorePublicAcls", "BlockPublicPolicy", "RestrictPublicBuckets")
    if not all(block.get(name) is True for name in fields):
        raise DependencyError("Bucket does not block all public access paths")
    status = aws("get-bucket-policy-status").get("PolicyStatus", {})
    if status.get("IsPublic") is not False:
        raise DependencyError("Private bucket policy not proven")
    acl = aws("get-bucket-acl")
    if any("AllUsers" in str(g.get("Grantee", {}).get("URI", "")) or
           "AuthenticatedUsers" in str(g.get("Grantee", {}).get("URI", ""))
           for g in acl.get("Grants", [])):
        raise DependencyError("Public object/bucket ACL grant detected")
    if aws("get-bucket-versioning").get("Status") != "Enabled":
        raise DependencyError("S3 versioning must be enabled before release")
    if not aws("get-bucket-encryption").get("ServerSideEncryptionConfiguration", {}).get("Rules"):
        raise DependencyError("At-rest S3 encryption is not configured")
    return {"tls": True, "private": True, "versioned": True, "encrypted": True}


def clamav_stream(host: str, port: int, payload: bytes) -> str:
    with socket.create_connection((host, port), timeout=7) as connection:
        connection.settimeout(12)
        connection.sendall(b"zINSTREAM\0" + struct.pack("!I", len(payload)) +
                           payload + struct.pack("!I", 0))
        output = bytearray()
        while len(output) < 8192:
            data = connection.recv(1024)
            if not data:
                break
            output.extend(data)
            if b"\0" in data:
                break
        return output.decode("utf-8", errors="replace").strip("\0\n")


def scanner_clean_and_infected() -> dict:
    host = os.environ.get("UAT_SCANNER_HOST", "")
    if not host or host in ("localhost", "127.0.0.1"):
        raise DependencyError("UAT scanner must use a configured private service host")
    port = int(os.environ.get("UAT_SCANNER_PORT", "3310"))
    clean = clamav_stream(host, port, b"DECONT clean test receipt payload")
    infected = clamav_stream(host, port, EICAR)
    if not clean.endswith("OK") or "FOUND" not in infected:
        raise DependencyError("ClamAV failed clean/infected in-memory checks")
    return {"clean_allowed": True, "eicar_blocked": True}


def ocr_engine_ready() -> dict:
    url = os.environ.get("UAT_OCR_URL", "")
    parsed = urlsplit(url)
    if parsed.scheme not in ("http", "https") or not parsed.hostname:
        raise DependencyError("OCR UAT service URL is missing")
    # The service may expose private HTTP only within the isolated network.
    with urlopen(Request(url.rstrip("/") + "/health", headers={"Accept": "application/json"}),
                 timeout=15) as response:
        state = json.load(response)
    if state.get("status") != "Ready" or not state.get("engine"):
        raise DependencyError("OCR model is not ready in UAT")
    return {"ready": True, "engine": str(state["engine"])[:50]}


def smtp_tls_test(send: bool) -> dict:
    host = os.environ.get("UAT_SMTP_HOST", "")
    from_address = os.environ.get("UAT_SMTP_FROM", "")
    destination = os.environ.get("UAT_SMTP_RECIPIENT", "")
    if not host or not from_address:
        raise DependencyError("SMTP host/sender missing")
    if send and (not destination or "@" not in destination):
        raise DependencyError("Test delivery requires UAT_SMTP_RECIPIENT")
    port = int(os.environ.get("UAT_SMTP_PORT", "587"))
    with smtplib.SMTP(host, port, timeout=15) as session:
        session.ehlo()
        if not session.has_extn("starttls"):
            raise DependencyError("SMTP server does not advertise STARTTLS")
        session.starttls(context=ssl.create_default_context())
        session.ehlo()
        username = os.environ.get("UAT_SMTP_USERNAME")
        password = os.environ.get("UAT_SMTP_PASSWORD")
        if username and password:
            session.login(username, password)
        if send:
            message = EmailMessage()
            message["Subject"] = "[DECONT UAT] Deliverability check"
            message["From"] = from_address
            message["To"] = destination
            message.set_content("Isolated Production-mode UAT notification delivery test.")
            session.send_message(message)
    return {"starttls": True, "test_email_accepted": send,
            "inbox_verification_required": True}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--uat-origin", required=True)
    parser.add_argument("--confirm-email", action="store_true")
    parser.add_argument("--report", required=True)
    args = parser.parse_args(argv)
    report_path = Path(args.report)
    if report_path.exists():
        print("Refusing to overwrite previous dependency evidence", file=sys.stderr)
        return 1
    result = {"status": "blocked", "release_approved": False}
    try:
        url = urlsplit(args.uat_origin)
        if url.scheme != "https" or not isolated_host(args.uat_origin):
            raise DependencyError("Dependency probes must use a real isolated HTTPS UAT hostname")
        details = {
            "postgres": postgres_verified_tls(),
            "s3": s3_private_versioned(),
            "scanner": scanner_clean_and_infected(),
            "ocr": ocr_engine_ready(),
            "smtp": smtp_tls_test(args.confirm_email),
        }
        result = {"status": "real_dependencies_checked_requires_business_workflows",
                  "checks": details, "release_approved": False}
        code = 0
    except (DependencyError, OSError, ValueError, KeyError, json.JSONDecodeError,
            ssl.SSLError, smtplib.SMTPException) as err:
        # Never print credentials, connection strings or third-party error bodies.
        result["reason"] = str(err) if isinstance(err, DependencyError) else "Dependency verification failed"
        code = 1
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    report_path.chmod(0o600)
    print("Dependencies UAT: " + result["status"])
    return code


if __name__ == "__main__":
    sys.exit(main())
