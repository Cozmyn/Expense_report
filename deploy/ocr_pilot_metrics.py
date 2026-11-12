#!/usr/bin/env python3
"""Privacy-minimizing acceptance report for the audit's real-document OCR pilot.

Reads 100-200 locally annotated documents, counts all T01-T22 test scenarios,
and calculates transparent per-field accuracy and actual review time. Does NOT
generate labels, infer ground truth, upload documents or claim existing accuracy.
Results contain aggregates only; raw supplier data never enters the report.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
from decimal import Decimal, InvalidOperation
import json
import math
from pathlib import Path
import statistics
import sys


FIELDS = ("amount", "date", "currency", "supplier", "vat")
SCENARIOS = tuple("T" + str(i).zfill(2) for i in range(1, 23))


class PilotError(ValueError):
    pass


def normalized(key: str, value):
    if value is None:
        return None
    if key == "amount":
        try:
            return Decimal(str(value).replace(",", "."))
        except InvalidOperation:
            return None
    if key == "vat":
        try:
            return tuple(sorted((str(item["rate"]), Decimal(str(item["base"])),
                                 Decimal(str(item["vat"]))) for item in value))
        except (TypeError, KeyError, InvalidOperation):
            return None
    return " ".join(str(value).upper().split())


def percentile(values: list[float], fraction: float) -> float:
    if not values:
        raise PilotError("No measured documents")
    ordered = sorted(values)
    # Nearest-rank percentile, stable and reproducible without extra packages.
    return round(ordered[max(0, math.ceil(len(ordered) * fraction) - 1)], 2)


def analyze(records: list[dict]) -> dict:
    if not 100 <= len(records) <= 200:
        raise PilotError("Pilot requires 100-200 representative REAL documents")
    seen = set()
    scenarios = set()
    time_used, manual_time = [], []
    corrections = 0
    omissions = joined = split = 0
    matching = {field: 0 for field in FIELDS}
    groups = defaultdict(list)
    for doc in records:
        if doc.get("source") != "real":
            raise PilotError("Synthetic fixtures cannot be included in the real-document pilot")
        code = doc.get("scenario")
        if code not in SCENARIOS:
            raise PilotError("Unknown acceptance scenario")
        scenarios.add(code)
        doc_id = doc.get("id")
        if not isinstance(doc_id, str) or not doc_id or doc_id in seen:
            raise PilotError("Pilot document IDs must be unique and non-empty")
        seen.add(doc_id)
        expected = doc.get("ground_truth")
        proposed = doc.get("ocr")
        if not isinstance(expected, dict) or not isinstance(proposed, dict):
            raise PilotError("Both adjudicated ground truth and OCR proposals are required")
        if any(key not in expected for key in FIELDS):
            raise PilotError("Missing adjudicated field in ground truth")
        for field in FIELDS:
            matching[field] += normalized(field, expected[field]) == normalized(
                field, proposed.get(field)) and expected[field] is not None
        seconds, baseline = doc.get("verification_seconds"), doc.get("manual_seconds")
        if not isinstance(seconds, (int, float)) or not 0 < seconds < 14400:
            raise PilotError("Verification duration must be measured and positive")
        if not isinstance(baseline, (int, float)) or not 0 < baseline < 14400:
            raise PilotError("Comparable manual entry baseline is required")
        time_used.append(float(seconds))
        manual_time.append(float(baseline))
        for key in ("corrections", "omitted_receipts", "wrongly_merged", "wrongly_split"):
            number = doc.get(key)
            if not isinstance(number, int) or number < 0:
                raise PilotError("Manual corrections and segmentation errors must be nonnegative integers")
        corrections += doc["corrections"]
        omissions += doc["omitted_receipts"]
        joined += doc["wrongly_merged"]
        split += doc["wrongly_split"]
        supplier = doc.get("supplier_frequency")
        if supplier not in ("rare", "frequent"):
            raise PilotError("Each document must identify rare or frequent supplier cohort")
        if type(doc.get("template_used")) is not bool:
            raise PilotError("Template usage must be explicitly recorded")
        groups[(supplier, "template" if doc["template_used"] else "no_template")].append(float(seconds))
    missing = sorted(set(SCENARIOS) - scenarios)
    if missing:
        raise PilotError("Missing required audit acceptance scenario coverage: " + ", ".join(missing))
    median = statistics.median(time_used)
    manual_median = statistics.median(manual_time)
    improvement = 1 - median / manual_median
    return {
        "status": "pilot_measured_no_automatic_release_approval",
        "documents": len(records),
        "scenarios_covered": sorted(scenarios),
        "fields": {name: {
            "correct": matching[name], "total": len(records),
            "accuracy": round(matching[name] / len(records), 4),
        } for name in FIELDS},
        "segmentation_errors": {
            "omitted_receipts": omissions, "wrongly_merged": joined,
            "wrongly_split": split,
        },
        "review_seconds": {"median": round(median, 2), "p90": percentile(time_used, .9)},
        "manual_baseline_seconds": {"median": round(manual_median, 2),
                                    "p90": percentile(manual_time, .9)},
        "manual_corrections": corrections,
        "supplier_cohorts": {
            cohort + "/" + variant: {"documents": len(times),
                                     "median_seconds": round(statistics.median(times), 2),
                                     "p90_seconds": percentile(times, .9)}
            for (cohort, variant), times in sorted(groups.items())
        },
        "median_time_reduction_fraction": round(improvement, 4),
        "negotiable_30_percent_target_met": improvement >= .30,
        "target_is_not_past_performance": True,
        "release_approved": False,
        "requires": "Human Finance/Operations review of discrepancies, unseen receipts and privacy/representativeness",
    }


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--jsonl", required=True,
                        help="Local redacted records; never upload raw receipts or identities")
    parser.add_argument("--report", required=True)
    args = parser.parse_args(argv)
    output = Path(args.report)
    if output.exists():
        print("Refusing to overwrite existing pilot evidence", file=sys.stderr)
        return 1
    try:
        lines = Path(args.jsonl).read_text(encoding="utf-8").splitlines()
        records = [json.loads(line) for line in lines if line.strip()]
        result = analyze(records)
        code = 0
    except (PilotError, OSError, ValueError, json.JSONDecodeError) as exc:
        result = {
            "status": "blocked",
            "reason": str(exc) if isinstance(exc, PilotError) else "Invalid pilot input",
            "release_approved": False,
        }
        code = 1
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    output.chmod(0o600)
    print("OCR pilot: " + result["status"] + "; report: " + str(output))
    return code


if __name__ == "__main__":
    raise SystemExit(main())
