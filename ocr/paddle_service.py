"""Internal-only PaddleOCR v6 adapter.

Images never leave this container. No receipt text or pixels are logged.
One request at a time protects the CPU model and keeps memory bounded.
"""
from __future__ import annotations

import json
import os
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
from io import BytesIO

MAX_BODY = 25 * 1024 * 1024
MODEL_NAME = "PP-OCRv6"
_LOCK = threading.Lock()
_PIPELINE = None


def normalize_result(result: dict, width: int, height: int) -> list[dict]:
    """Use PaddleOCR's documented rec_texts, rec_scores and rec_boxes arrays."""
    if width <= 0 or height <= 0:
        raise ValueError("Invalid image dimensions")
    data = result.get("res", result)
    texts = data.get("rec_texts", [])
    scores = data.get("rec_scores", [])
    boxes = data.get("rec_boxes", [])
    if not (len(texts) == len(scores) == len(boxes)):
        raise ValueError("Inconsistent OCR prediction arrays")
    words = []
    for text, score, box in zip(texts, scores, boxes):
        if not isinstance(text, str) or not text.strip():
            continue
        if len(box) != 4:
            continue
        left, top, right, bottom = map(float, box)
        x = max(0.0, min(1.0, left / width))
        y = max(0.0, min(1.0, top / height))
        rx = max(0.0, min(1.0, right / width))
        by = max(0.0, min(1.0, bottom / height))
        if rx <= x or by <= y:
            continue
        words.append({
            "text": text[:1000], "x": x, "y": y,
            "width": rx - x, "height": by - y,
            "confidence": max(0.0, min(1.0, float(score))),
        })
    # Retain reading order within lines, and page coordinates for highlights.
    return sorted(words, key=lambda w: (round(w["y"], 2), w["x"]))


class Handler(BaseHTTPRequestHandler):
    server_version = "ExpensePaddleOCR/1"

    def log_message(self, fmt, *args):
        # Deliberately no URLs, request bodies or document information in logs.
        pass

    def respond(self, status: int, data: dict):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path == "/health":
            self.respond(200, {"status": "Ready", "engine": MODEL_NAME, "languages": ["ro", "en"]})
        else:
            self.respond(404, {"error": "Not found"})

    def do_POST(self):
        if self.path != "/ocr":
            self.respond(404, {"error": "Not found"})
            return
        length = self.headers.get("Content-Length", "")
        if not length.isdigit() or not 0 < int(length) <= MAX_BODY:
            self.respond(413, {"error": "Image exceeds configured size"})
            return
        if self.headers.get("Content-Type", "").split(";")[0].strip() != "application/octet-stream":
            self.respond(415, {"error": "Expected image bytes"})
            return
        try:
            from PIL import Image, ImageOps
            import numpy as np

            source = self.rfile.read(int(length))
            with Image.open(BytesIO(source)) as raw:
                image = ImageOps.exif_transpose(raw).convert("RGB")
                if image.width * image.height > 45_000_000:
                    self.respond(413, {"error": "Image too large"})
                    return
                size = image.size
                pixels = np.array(image)
            with _LOCK:
                predictions = _PIPELINE.predict(pixels)
            items = list(predictions)
            if len(items) != 1:
                raise ValueError("Expected one image prediction")
            self.respond(200, {
                "engine": MODEL_NAME, "width": size[0], "height": size[1],
                "words": normalize_result(items[0].json, *size),
            })
        except Exception:
            # Fail closed without returning source image, raw text or stack traces.
            self.respond(422, {"error": "Image recognition failed"})


def main():
    global _PIPELINE
    from paddleocr import PaddleOCR

    _PIPELINE = PaddleOCR(
        lang="ro", ocr_version=MODEL_NAME,
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=True,
        device="cpu",
    )
    port = int(os.environ.get("OCR_PORT", "8750"))
    HTTPServer(("0.0.0.0", port), Handler).serve_forever()


if __name__ == "__main__":
    main()
