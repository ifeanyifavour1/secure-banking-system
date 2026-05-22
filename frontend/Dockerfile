# Banking frontend: nginx (internet-facing) → Gunicorn (loopback) → Flask
FROM python:3.12-slim-bookworm

RUN apt-get update \
    && apt-get install -y --no-install-recommends nginx \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .
RUN chmod +x /app/docker/start.sh

ENV PYTHONUNBUFFERED=1
ENV FLASK_ENV=production
ENV GUNICORN_BIND=127.0.0.1:8001

EXPOSE 10000

CMD ["/app/docker/start.sh"]
