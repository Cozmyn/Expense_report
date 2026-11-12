#!/usr/bin/env python3
"""Aggregate real P0 evidence. Never approve Production from offline CI alone.

All JSON inputs are produced by isolated UAT runs or an accountable operator.
A passing report is a review packet, not automated authorization to deploy.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import sys


REQUIRED_SCENARIOS = (
    "postgres_tls_verify_full", "migration_from_existing_database", "schema_constraints",
    "same_image_digest", "private_s3", "scanner_clean", "scanner_infected",
    "ocr_real_document", "receipt_review", "login", "password_reset_email",
    "smtp_delivery", "manager_approval", "finance_export_xlsx",
    "report_pdf", "restart", "rollback_old_image", "rollback_data_consistency",
    "proxy_peer_validation", "ingress_rate_limit", "data_protection_keys",
)


class GateError(RuntimeError):
    pass


def read(directory: Path, name: str) -> dict:
    path = directory / name
    if path.is_symlink() or not path.is_file():
        raise GateError("Missing or unsafe required evidence file: " + name)
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise GateError("Evidence is not a JSON object: " + name)
    return data


def evaluate(folder: Path) -> dict:
    restoration = read(folder, "restore.json")
    initial = read(folder, "uat-initial.json")
    resumed = read(folder, "uat-resume.json")
    ingress = read(folder, "uat-ingress.json")
    workflows = read(folder, "workflows.json")
    if restoration.get("status") != "integrity_verified_requires_application_uat":
        raise GateError("Restored PostgreSQL, objects and keys have not passed the coordinated drill")
    if not all(int(restoration.get(key, 0)) >= 1 for key in
               ("objects_verified", "exports_verified", "data_protection_keys_verified")):
        raise GateError("Restore has not verified all evidence types")
    for name, item in (("initial", initial), ("resume", resumed), ("ingress", ingress)):
        if item.get("status") != "checks_passed_requires_full_workflow_uat" or item.get("mode") != (
            "resume" if name == "resume" else "ingress" if name == "ingress" else "initial"
        ):
            raise GateError("Missing actual Production-mode HTTPS evidence for " + name)
    expected = workflows.get("built_image_digest")
    deployed = workflows.get("tested_image_digest")
    promotion = workflows.get("promoted_image_digest")
    if (not isinstance(expected, str) or not re.fullmatch(r"sha256:[0-9a-f]{64}", expected)
            or expected != deployed or expected != promotion):
        raise GateError("One immutable OCI image digest must match build, UAT and promotion")
    steps = workflows.get("scenarios")
    if not isinstance(steps, dict):
        raise GateError("Scenario evidence is missing")
    missing = []
    for name in REQUIRED_SCENARIOS:
        info = steps.get(name)
        if (not isinstance(info, dict) or info.get("status") != "passed"
                or not isinstance(info.get("artifact"), str) or len(info["artifact"]) < 8):
            missing.append(name)
    if missing:
        raise GateError("Unverified required P0 scenarios: " + ", ".join(missing))
    return {
        "status": "complete_evidence_packet_requires_named_business_and_ops_approval",
        "release_approved": False,
        "immutable_image_digest": expected,
        "tested_scenarios": len(REQUIRED_SCENARIOS),
        "restore_rpo_minutes": restoration.get("rpo_minutes"),
        "restore_rto_seconds": restoration.get("duration_seconds"),
        "note": "Approval is external to this tool. Confirm Finance, Security and Operations signoff.",
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence-dir", required=True)
    parser.add_argument("--report", required=True)
    args = parser.parse_args(argv)
    output = Path(args.report)
    if output.exists():
        print("Refusing to overwrite existing release-gate evidence", file=sys.stderr)
        return 1
    try:
        result = evaluate(Path(args.evidence_dir))
        code = 0
    except (GateError, OSError, ValueError, json.JSONDecodeError) as err:
        result = {"status": "blocked", "reason": str(err) if isinstance(err, GateError)
                  else "Malformed release evidence", "release_approved": False}
        code = 1
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    output.chmod(0o600)
    print("P0 release evidence: " + result["status"])
    return code


if __name__ == "__main__":
    sys.exit(main())
