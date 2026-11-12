#!/usr/bin/env python3
"""Destructive actions are restricted to fresh isolated UAT targets.

Restore a PostgreSQL custom-format dump plus an immutable object snapshot and
ASP.NET Data Protection key ring, then verify *every* object referenced in the
restored database against the persisted SHA-256. No production source is touched.
The report is evidence of a particular rehearsal, not an automatic release signoff.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone
from urllib.parse import urlsplit


class RecoveryError(RuntimeError):
    pass


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest().upper()


def verified_file(root: Path, key: str) -> Path:
    # S3 keys in DECONT are structured path components, never absolute paths.
    parts = key.split("/")
    if not parts or any(not re.fullmatch(r"[A-Za-z0-9_.-]+", p) or p in (".", "..") for p in parts):
        raise RecoveryError("Invalid object/key path in backup manifest")
    if root.is_symlink():
        raise RecoveryError("Backup root must not be a symlink")
    path = root.joinpath(*parts)
    if not path.resolve().is_relative_to(root.resolve()):
        raise RecoveryError("Path escaped snapshot root")
    if path.is_symlink() or not path.is_file():
        raise RecoveryError("Missing object or symbolic-link object in snapshot")
    return path


def command(argv: list[str]) -> str:
    try:
        result = subprocess.run(argv, capture_output=True, text=True, check=True)
        return result.stdout
    except (OSError, subprocess.CalledProcessError) as e:
        # Do not leak SQL, credentials, bucket keys or CLI arguments in logs.
        raise RecoveryError("External command failed: " + argv[0]) from e


def aws(endpoint: str, *args: str) -> str:
    if not (endpoint.startswith("https://") and urlsplit(endpoint).hostname):
        raise RecoveryError("S3 endpoint must use verified HTTPS")
    return command(["aws", "--endpoint-url", endpoint, "s3api", *args])


def psql(db_name: str, sql: str) -> str:
    return command(["psql", "-X", "-q", "-v", "ON_ERROR_STOP=1", "-d", db_name, "-c", sql])


def require_isolated(db_name: str, bucket: str, confirm: str) -> None:
    if not re.fullmatch(r"decont_restore_[a-z0-9_]{4,50}", db_name):
        raise RecoveryError("Target database must be named decont_restore_<unique>")
    if not re.fullmatch(r"decont-restore-[a-z0-9-]{4,55}", bucket):
        raise RecoveryError("Target bucket must be named decont-restore-<unique>")
    if confirm != "RESTORE_TO_ISOLATED_UAT_ONLY":
        raise RecoveryError("Explicit isolated-target confirmation required")


def read_manifest(path: Path, dump: Path, keys_root: Path, pitr: bool = False) -> dict:
    manifest = json.loads(path.read_text(encoding="utf-8"))
    if sha256(dump) != str(manifest.get("database_sha256", "")).upper():
        raise RecoveryError("PostgreSQL backup SHA-256 differs from immutable manifest")
    if not pitr:
        with dump.open("rb") as stream:
            if stream.read(5) != b"PGDMP":
                raise RecoveryError("Expected a PostgreSQL custom-format archive")
    keys = manifest.get("keys")
    if not isinstance(keys, dict) or not keys:
        raise RecoveryError("Data Protection key-ring manifest must contain keys")
    for name, expected in keys.items():
        if not re.fullmatch(r"key-[A-Za-z0-9-]+\.xml", name):
            raise RecoveryError("Unexpected Data Protection key filename")
        if sha256(verified_file(keys_root, name)) != str(expected).upper():
            raise RecoveryError("Data Protection key backup hash mismatch")
    try:
        snap = datetime.fromisoformat(manifest["snapshot_utc"].replace("Z", "+00:00"))
        if snap.tzinfo is None:
            raise ValueError
    except (KeyError, ValueError) as e:
        raise RecoveryError("Backup snapshot_utc must include an explicit timezone") from e
    return manifest


def refs_from_database(db: str) -> list[dict]:
    # Include archived originals and immutable exported workbooks, not only
    # current/clean documents. The DB is restored into a dedicated empty target.
    sql = """COPY (
      SELECT 'attachment' AS kind, "Id"::text AS id, "Key" AS key,
             "Hash" AS sha, "Size"::bigint AS size FROM "ReportAttachments"
      UNION ALL
      SELECT 'evidence', "Id"::text, "Key", "Hash", "Size"::bigint FROM "Evidence"
      UNION ALL
      SELECT 'batch', "Id"::text, "Key", "FileHash", -1::bigint FROM "Batches"
    ) TO STDOUT WITH (FORMAT CSV, HEADER TRUE)"""
    return list(csv.DictReader(io.StringIO(psql(db, sql))))


def verify_private_bucket(endpoint: str, bucket: str) -> None:
    acl = json.loads(aws(endpoint, "get-bucket-acl", "--bucket", bucket))
    grants = acl.get("Grants", [])
    for grant in grants:
        uri = str(grant.get("Grantee", {}).get("URI", ""))
        if "AllUsers" in uri or "AuthenticatedUsers" in uri:
            raise RecoveryError("Restore target bucket has a public ACL")
    # For AWS buckets verify policy visibility too. An unsupported S3-compatible
    # API fails closed: the operator must provide a compatible privacy verifier.
    status = json.loads(aws(endpoint, "get-bucket-policy-status", "--bucket", bucket))
    if status.get("PolicyStatus", {}).get("IsPublic") is not False:
        raise RecoveryError("Cannot prove the restored S3 bucket policy is private")


def verify_empty_targets(db: str, endpoint: str, bucket: str, keys_dest: Path) -> None:
    count = psql(db,
        "SELECT count(*) FROM information_schema.tables "
        "WHERE table_schema=current_schema() AND table_type='BASE TABLE'")
    if count.strip() != "0":
        raise RecoveryError("Refusing to restore over a non-empty database")
    listing = json.loads(aws(endpoint, "list-objects-v2", "--bucket", bucket, "--max-keys", "1"))
    if listing.get("KeyCount", 0) or listing.get("Contents"):
        raise RecoveryError("Refusing to overwrite a non-empty restore bucket")
    if keys_dest.exists() and any(keys_dest.iterdir()):
        raise RecoveryError("Refusing to overwrite an existing key ring")


def copy_keys(source: Path, target: Path, manifest: dict) -> int:
    target.mkdir(mode=0o700, parents=True, exist_ok=False)
    os.chmod(target, 0o700)
    for filename, expected in manifest["keys"].items():
        path = verified_file(source, filename)
        destination = target / filename
        with destination.open("xb") as output, path.open("rb") as inp:
            shutil.copyfileobj(inp, output)
        destination.chmod(0o600)
        if sha256(destination) != expected.upper():
            raise RecoveryError("Restored Data Protection key does not match backup")
    return len(manifest["keys"])


def restore_objects(rows: list[dict], root: Path, endpoint: str, bucket: str,
                    already_restored: bool = False) -> tuple[int, int, int]:
    seen: dict[str, str] = {}
    counts = {"attachment": 0, "evidence": 0, "batch": 0}
    size_total = 0
    for row in rows:
        kind = row["kind"]
        if kind not in counts:
            raise RecoveryError("Unknown stored object type")
        counts[kind] += 1
        key = row["key"]
        digest = row["sha"].upper()
        if not key or not re.fullmatch("[A-F0-9]{64}", digest):
            raise RecoveryError("Missing key or SHA-256 in restored financial data")
        if key in seen:
            if seen[key] != digest:
                raise RecoveryError("Conflicting stored hashes for the same object")
            continue
        if not already_restored:
            source = verified_file(root, key)
            if sha256(source) != digest:
                raise RecoveryError("Original or export bytes mismatch the restored database")
            if row["size"] != "-1" and source.stat().st_size != int(row["size"]):
                raise RecoveryError("Original document size differs from persisted size")
            aws(endpoint, "put-object", "--bucket", bucket, "--key", key,
                "--body", str(source), "--server-side-encryption", "AES256")
        # Round trip through the actual recovered S3 bucket; neither ETag nor a
        # 200 from PUT proves object integrity for multi-part/encrypted objects.
        with tempfile.TemporaryDirectory(prefix="decont-restored-") as work:
            download = Path(work) / "object"
            aws(endpoint, "get-object", "--bucket", bucket, "--key", key, str(download))
            if sha256(download) != digest:
                raise RecoveryError("Restored S3 object SHA-256 does not match database")
            if row["size"] != "-1" and download.stat().st_size != int(row["size"]):
                raise RecoveryError("Restored S3 object size does not match database")
            size_total += download.stat().st_size
        seen[key] = digest
    if sum(counts.values()) == 0 or counts["batch"] == 0:
        raise RecoveryError("Acceptance fixture requires original documents and at least one export")
    return len(seen), size_total, counts["batch"]


def run(args: argparse.Namespace) -> dict:
    started = time.monotonic()
    dump = Path(args.database_dump)
    keys = Path(args.backup_keys)
    objects = Path(args.backup_objects)
    manifest = read_manifest(Path(args.manifest), dump, keys, args.verify_existing)
    if not args.execute:
        return {"status": "backup_integrity_only", "dump_sha256": sha256(dump),
                "data_protection_keys": len(manifest["keys"]), "release_approved": False}
    require_isolated(args.database, args.bucket, args.confirm)
    if not args.verify_existing and not objects.is_dir():
        raise RecoveryError("Object snapshot directory is unavailable")
    if not args.incident_utc or not args.max_rpo_minutes or not args.max_rto_seconds:
        raise RecoveryError("Business-approved incident time, RPO and RTO limits are mandatory")
    incident = datetime.fromisoformat(args.incident_utc.replace("Z", "+00:00"))
    snapshot = datetime.fromisoformat(manifest["snapshot_utc"].replace("Z", "+00:00"))
    rpo = (incident - snapshot).total_seconds() / 60.0
    if not 0 <= rpo <= args.max_rpo_minutes:
        raise RecoveryError("Restore point exceeds business-approved RPO")
    target_keys = Path(args.target_keys)
    verify_private_bucket(args.s3_endpoint, args.bucket)
    if args.verify_existing:
        # After an operator-run WAL/PITR restore, verify in place without
        # re-uploading originals. Include the external recovery time.
        if not args.restore_start_utc:
            raise RecoveryError("PITR verification requires the external restore start time")
        since = datetime.fromisoformat(args.restore_start_utc.replace("Z", "+00:00"))
        elapsed = (datetime.now(timezone.utc) - since).total_seconds()
        if elapsed < 0:
            raise RecoveryError("Restore start cannot be in the future")
        key_count = 0
        for name, expected in manifest["keys"].items():
            if sha256(verified_file(target_keys, name)) != expected.upper():
                raise RecoveryError("PITR recovered key-ring differs from backup")
            key_count += 1
    else:
        if target_keys.exists():
            raise RecoveryError("Data Protection restore destination must not exist")
        verify_empty_targets(args.database, args.s3_endpoint, args.bucket, target_keys)
        command(["pg_restore", "--dbname", args.database, "--exit-on-error",
                 "--single-transaction", "--no-owner", "--no-privileges", str(dump)])
    rows = refs_from_database(args.database)
    object_count, object_bytes, export_count = restore_objects(
        rows, objects, args.s3_endpoint, args.bucket, args.verify_existing)
    if not args.verify_existing:
        key_count = copy_keys(keys, target_keys, manifest)
        elapsed = time.monotonic() - started
    else:
        elapsed = (datetime.now(timezone.utc) - since).total_seconds()
    if elapsed > args.max_rto_seconds:
        raise RecoveryError("Recovery duration exceeds business-approved RTO")
    return {
        "status": "integrity_verified_requires_application_uat",
        "database": args.database, "bucket": args.bucket,
        "dump_sha256": sha256(dump), "objects_verified": object_count,
        "object_bytes": object_bytes, "exports_verified": export_count,
        "data_protection_keys_verified": key_count,
        "duration_seconds": round(elapsed, 2), "rpo_minutes": round(rpo, 2),
        "pitr_verified": bool(args.verify_existing),
        "release_approved": False,
        "requires": "Restart application with restored keys; verify login, evidence, PDF, XLSX and Finance approval in HTTPS UAT",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("database-dump", "backup-objects", "backup-keys", "manifest"):
        parser.add_argument("--" + name, required=True)
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--verify-existing", action="store_true",
                        help="Verify an already completed isolated PostgreSQL PITR + S3 + key restore.")
    parser.add_argument("--restore-start-utc",
                        help="UTC timestamp when the external PITR recovery began.")
    parser.add_argument("--database", default="")
    parser.add_argument("--bucket", default="")
    parser.add_argument("--s3-endpoint", default="")
    parser.add_argument("--target-keys", default="")
    parser.add_argument("--confirm", default="")
    parser.add_argument("--incident-utc")
    parser.add_argument("--max-rpo-minutes", type=float)
    parser.add_argument("--max-rto-seconds", type=float)
    parser.add_argument("--report", required=True)
    args = parser.parse_args(argv)
    try:
        result = run(args)
        code = 0
    except (RecoveryError, OSError, ValueError, json.JSONDecodeError) as error:
        result = {"status": "failed", "code": type(error).__name__,
                  "message": str(error) if isinstance(error, RecoveryError) else "Invalid restore input",
                  "release_approved": False}
        code = 1
    destination = Path(args.report)
    if destination.exists():
        print("Refusing to overwrite previous restore evidence", file=sys.stderr)
        return 1
    destination.parent.mkdir(parents=True, exist_ok=True)
    with destination.open("x", encoding="utf-8") as stream:
        os.chmod(destination, 0o600)
        json.dump(result, stream, ensure_ascii=False, indent=2)
    print("Restore drill: " + result["status"] + "; evidence: " + str(destination))
    return code


if __name__ == "__main__":
    sys.exit(main())
