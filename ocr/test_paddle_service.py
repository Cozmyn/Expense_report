"""Unit tests run without installing model weights or transferring real documents."""
import unittest
from paddle_service import normalize_result


class AdapterTests(unittest.TestCase):
    def test_normalized_coordinates_and_confidence(self):
        source = {"res": {"rec_texts": ["Bon fiscal", "TOTAL 123,45 RON"],
                          "rec_scores": [0.99, 0.75],
                          "rec_boxes": [[10, 10, 100, 30], [20, 60, 180, 80]]}}
        words = normalize_result(source, 200, 100)
        self.assertEqual([w["text"] for w in words], ["Bon fiscal", "TOTAL 123,45 RON"])
        self.assertAlmostEqual(words[1]["x"], .1)
        self.assertAlmostEqual(words[1]["y"], .6)
        self.assertAlmostEqual(words[1]["width"], .8)
        self.assertEqual(words[1]["confidence"], .75)

    def test_bad_or_missing_boxes_cannot_be_used(self):
        self.assertEqual([], normalize_result({"rec_texts": [""], "rec_scores": [.4],
                                                 "rec_boxes": [[0, 0, 2, 2]]}, 10, 10))
        with self.assertRaises(ValueError):
            normalize_result({"rec_texts": ["one"], "rec_scores": [], "rec_boxes": []}, 200, 100)

    def test_confidence_and_geometry_are_bounded(self):
        sample = {"rec_texts": ["A"], "rec_scores": [5],
                  "rec_boxes": [[-10, -10, 300, 200]]}
        result = normalize_result(sample, 200, 100)
        self.assertEqual(result[0]["confidence"], 1)
        self.assertEqual(result[0]["x"], 0)
        self.assertEqual(result[0]["width"], 1)


if __name__ == "__main__":
    unittest.main()
