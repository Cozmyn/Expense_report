#!/usr/bin/env python3
"""Read-only production configuration preflight for the expense platform.

Does not connect to databases, read .env files, print secret values, or deploy.
Passing this preflight is NOT production approval; manual gates remain mandatory.
"""

from __future__ import annotations

import argparse
import ipaddress
import json
import os
import sys
from dataclasses import asdict, dataclass
from typing import Mapping
from urllib.parse import urlsplit


@dataclass(frozen=True)
class Finding:
    code: str
    severity: str
    message: str


def _value(config: Mapping[str, str], key: str) -> str:
    return str(config.get(key) or "").strip()


def _placeholder(value: str) -> bool:
    lowered = value.casefold()
    return any(part in lowered for part in ("replace-with", "changeme", "example-secret", "dummy-password"))


def _connection_parts(connection: str) -> dict[str, str]:
    entries: dict[str, str] = {}
    for entry in connection.split(";"):
        if "=" in entry:
            key, value = entry.split("=", 1)
            entries[key.strip().casefold().replace(" ", "")] = value.strip()
    return entries


def _is_https_origin(value: str) -> bool:
    try:
        parsed = urlsplit(value)
        return (
            parsed.scheme.casefold() == "https"
            and bool(parsed.hostname)
            and not parsed.username
            and not parsed.password
            and not parsed.query
            and not parsed.fragment
            and parsed.path in ("", "/")
        )
    except ValueError:
        return False


def _is_https_endpoint(value: str) -> bool:
    try:
        parsed = urlsplit(value)
        return (
            parsed.scheme.casefold() == "https"
            and bool(parsed.hostname)
            and not parsed.username
            and not parsed.password
        )
    except ValueError:
        return False



def _private_ocr_endpoint(value: str) -> bool:
    """Fail closed for public OCR URLs without contacting DNS or the service."""
    try:
        parsed = urlsplit(value)
        host = (parsed.hostname or "").lower().rstrip(".")
        if (
            parsed.scheme.lower() not in ("http", "https")
            or not host
            or parsed.username or parsed.password
            or parsed.query or parsed.fragment
            or parsed.path not in ("", "/")
            or parsed.port == 0
            or host in ("localhost", "0.0.0.0", "::", "127.0.0.1")
            or host.endswith(".localhost")
        ):
            return False
        try:
            ip = ipaddress.ip_address(host)
            return ip.is_private and not (ip.is_loopback or ip.is_link_local or ip.is_unspecified)
        except ValueError:
            pass
        # Docker/Kubernetes service names or explicitly internal DNS. Public
        # domains cannot be treated as private without a verified routing design.
        return (
            ("." not in host and host.replace("-", "").isalnum())
            or host.endswith((".internal", ".local", ".svc", ".svc.cluster.local"))
        )
    except (ValueError, TypeError):
        return False


def check(config: Mapping[str, str]) -> list[Finding]:
    results: list[Finding] = []

    def block(code: str, message: str) -> None:
        results.append(Finding(code, "blocker", message))

    def warn(code: str, message: str) -> None:
        results.append(Finding(code, "manual", message))

    if _value(config, "ASPNETCORE_ENVIRONMENT") != "Production":
        block("ENVIRONMENT", "Production deployment must set ASPNETCORE_ENVIRONMENT=Production.")
    if _value(config, "Database__Provider") != "Postgres":
        block("DATABASE_PROVIDER", "Production must use the PostgreSQL provider.")

    connection = _value(config, "ConnectionStrings__Database")
    if not connection:
        block("DATABASE_CONNECTION", "A PostgreSQL connection configuration is required.")
    else:
        properties = _connection_parts(connection)
        mode = properties.get("sslmode", "").casefold().replace("-", "").replace(" ", "")
        if mode not in ("verifyfull", "verifyca"):
            warn("DATABASE_TLS_VERIFICATION", "Confirm verified TLS and server identity for PostgreSQL; prefer SSL Mode=VerifyFull.")
        if properties.get("trustservercertificate", "").casefold() in ("true", "yes", "1"):
            block("DATABASE_TLS_BYPASS", "Do not bypass PostgreSQL server certificate validation.")
        if _placeholder(connection):
            block("DATABASE_PLACEHOLDER", "Replace placeholder database credentials before deployment.")

    origin = _value(config, "Application__BaseUrl")
    if not _is_https_origin(origin):
        block("PUBLIC_ORIGIN", "Application__BaseUrl must be an HTTPS origin without credentials, query or fragment.")

    if _value(config, "Storage__Provider") != "S3":
        block("PRIVATE_OBJECT_STORAGE", "Production requires Storage__Provider=S3.")
    endpoint = _value(config, "Storage__Endpoint")
    if not _is_https_endpoint(endpoint):
        block("STORAGE_TLS", "Storage__Endpoint must be an HTTPS URL for production.")
    if not _value(config, "Storage__Bucket"):
        block("STORAGE_BUCKET", "Set a dedicated private object storage bucket.")
    for field in ("Storage__AccessKey", "Storage__SecretKey"):
        credential = _value(config, field)
        if not credential:
            block("STORAGE_CREDENTIALS", "Configure object storage credentials using the approved secret provider.")
            break
        if _placeholder(credential):
            block("STORAGE_PLACEHOLDER", "Replace placeholder object storage credentials.")
            break

    if not _value(config, "Scanner__Host"):
        block("ANTIVIRUS_SCANNER", "Configure a reachable private antivirus scanning service.")

    if _value(config, "Ocr__Enabled").lower() != "true":
        block("OCR_DISABLED", "Set Ocr__Enabled=true for the approved PaddleOCR service.")
    endpoint = _value(config, "Ocr__ServiceUrl")
    if not endpoint:
        block("OCR_SERVICE_URL", "Set Ocr__ServiceUrl to an explicit private PaddleOCR endpoint.")
    elif not _private_ocr_endpoint(endpoint):
        block("OCR_PRIVATE_ENDPOINT", "OCR must use an internal service URL without credentials or public routing.")
    else:
        warn("OCR_SERVICE_VERIFICATION", "Confirm PaddleOCR PP-OCRv6 model cache, private routing, full extraction, image size, latency, CPU/memory, and failure alerts on the actual deployment.")

    if not _value(config, "Data__Directory"):
        block("DATA_PROTECTION_KEYS", "Configure a persistent protected directory for Data Protection keys.")
    else:
        warn("KEY_DURABILITY", "Verify that Data Protection keys survive rolling deployments and are backed up, encrypted, and permission-restricted.")

    session = _value(config, "Session__Minutes")
    if session:
        try:
            if int(session) <= 0:
                raise ValueError
        except ValueError:
            block("SESSION_TIMEOUT", "Session__Minutes must be a positive integer when set.")

    if not _value(config, "Smtp__Host") or not _value(config, "Smtp__From"):
        warn("EMAIL_RECOVERY", "Confirm SMTP TLS, password recovery and actual notification delivery.")

    # Source code cannot prove these external controls from environment variables.
    warn("TRUSTED_PROXY", "A trusted-forwarded-headers and TLS-terminating ingress design needs deployment review; source is not yet production-hardened for a generic reverse proxy.")
    warn("BACKUP_RESTORE", "Provide a timed PostgreSQL PITR, object storage, and Data Protection key restore rehearsal with agreed RPO/RTO.")
    warn("ERP_AND_OCR_ACCEPTANCE", "Finance must sign off real ERP golden files and representative OCR receipts.")
    warn("OPERATIONS", "Prove alerts, capacity, scanner downtime and incident runbooks on production-like infrastructure.")
    return results


def main(argv: list[str] | None = None, env: Mapping[str, str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true", help="Output redacted findings as JSON.")
    args = parser.parse_args(argv)
    findings = check(os.environ if env is None else env)
    blockers = [f for f in findings if f.severity == "blocker"]

    if args.json:
        print(json.dumps({
            "result": "blocked" if blockers else "configuration_preflight_passed_only",
            "blockers": len(blockers),
            "manual_reviews": len(findings) - len(blockers),
            "findings": [asdict(f) for f in findings],
        }, ensure_ascii=False, indent=2))
    else:
        print(f"Configuration preflight: {len(blockers)} blocker(s), {len(findings) - len(blockers)} manual review(s).")
        for f in findings:
            print(f"[{f.severity.upper()}] {f.code}: {f.message}")
        print("No secrets were printed. This check never certifies production readiness.")

    return 1 if blockers else 0


if __name__ == "__main__":
    sys.exit(main())
