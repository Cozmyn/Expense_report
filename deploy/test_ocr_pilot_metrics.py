"""Synthetic unit fixtures for the real-document pilot **calculator**, not OCR UAT."""
import unittest
from ocr_pilot_metrics import PilotError, analyze, percentile


def corpus():
    return [{
        "id": f"fixture-{i}",
        "source": "real",  # Input shape exercised; these tests are NOT real receipts.
        "scenario": f"T{(i % 22) + 1:02d}",
        "ground_truth": {"amount": "123.45", "date": "2026-11-11",
                         "currency": "RON", "supplier": "PRIVATE SUPPLIER NAME",
                         "vat": [{"rate": 21, "base": 100, "vat": 21}]},
        "ocr": {"amount": "123.45", "date": "2026-11-11",
                "currency": "RON", "supplier": "private supplier name",
                "vat": [{"rate": 21, "base": 100, "vat": 21}]},
        "verification_seconds": 60,
        "manual_seconds": 120,
        "corrections": 0, "omitted_receipts": 0,
        "wrongly_merged": 0, "wrongly_split": 0,
        "supplier_frequency": "rare" if i % 3 else "frequent",
        "template_used": bool(i % 2),
    } for i in range(100)]


class AuditPilotMetricsTests(unittest.TestCase):
    def test_all_22_scenarios_and_exact_field_accuracy_are_reported_without_private_data(self):
        docs = corpus()
        docs[0]["ocr"]["amount"] = "121.45"
        docs[1]["omitted_receipts"] = 1
        docs[2]["wrongly_merged"] = 2
        docs[3]["wrongly_split"] = 1
        docs[0]["corrections"] = 2
        result = analyze(docs)
        self.assertEqual(100, result["documents"])
        self.assertEqual(22, len(result["scenarios_covered"]))
        self.assertEqual(99, result["fields"]["amount"]["correct"])
        self.assertEqual(1, result["segmentation_errors"]["omitted_receipts"])
        self.assertEqual(2, result["manual_corrections"])
        self.assertEqual(60, result["review_seconds"]["median"])
        self.assertEqual(60, result["review_seconds"]["p90"])
        self.assertTrue(result["negotiable_30_percent_target_met"])
        self.assertFalse(result["release_approved"])
        self.assertNotIn("PRIVATE SUPPLIER NAME", str(result))

    def test_no_synthetic_or_small_corpus_can_pass_real_acceptance_gate(self):
        for bad in (corpus()[:99], corpus() + corpus() + corpus()[:1]):
            with self.assertRaisesRegex(PilotError, "100-200"):
                analyze(bad)
        docs = corpus()
        docs[0]["source"] = "synthetic"
        with self.assertRaisesRegex(PilotError, "Synthetic"):
            analyze(docs)

    def test_missing_scenario_or_invalid_duration_blocks(self):
        docs = corpus()
        for doc in docs:
            doc["scenario"] = "T01"
        with self.assertRaisesRegex(PilotError, "Missing required"):
            analyze(docs)
        docs = corpus()
        docs[10]["manual_seconds"] = 0
        with self.assertRaisesRegex(PilotError, "Comparable manual"):
            analyze(docs)

    def test_nearest_rank_p90_and_missing_field_fail_closed(self):
        self.assertEqual(9, percentile(list(range(1, 11)), .9))
        docs = corpus()
        del docs[0]["ground_truth"]["vat"]
        with self.assertRaisesRegex(PilotError, "adjudicated field"):
            analyze(docs)


if __name__ == "__main__":
    unittest.main()
