FROM python:3.12-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt && useradd --uid 10001 --create-home web
COPY . .
# static/shared/*.css and static/favicon.ico are generated from Farm/wwwroot; they are
# not tracked in git, so a fresh clone must stage them before the image is built.
RUN for asset in static/shared/variables.css static/shared/base.css static/favicon.ico; do \
        [ -f "$asset" ] || { \
            echo "ERROR: missing generated asset web/$asset." >&2; \
            echo "Run this from the repository root, then rebuild:" >&2; \
            echo "  python3 MailBox/scripts/stage-shared-assets.py" >&2; \
            exit 1; \
        }; \
    done
USER web
EXPOSE 8000
CMD ["gunicorn", "--bind", "0.0.0.0:8000", "--workers", "1", "--threads", "8", "--timeout", "90", "app:app"]
