FROM python:3.11-slim-bookworm
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PADDLE_PDX_MODEL_SOURCE=HUGGINGFACE \
    PADDLE_PDX_CACHE_HOME=/models \
    OCR_PORT=8750
WORKDIR /service
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgomp1 libgl1 libglib2.0-0 && rm -rf /var/lib/apt/lists/*
COPY ocr/requirements.txt .
RUN pip install --no-cache-dir --disable-pip-version-check -r requirements.txt
COPY ocr/paddle_service.py .
RUN mkdir -p /models && chown -R 10001:10001 /models /service
USER 10001
EXPOSE 8750
HEALTHCHECK --interval=30s --timeout=5s --start-period=120s --retries=5 \
    CMD python -c "from urllib.request import urlopen; urlopen('http://127.0.0.1:8750/health',timeout=3)" || exit 1
CMD ["python","paddle_service.py"]
