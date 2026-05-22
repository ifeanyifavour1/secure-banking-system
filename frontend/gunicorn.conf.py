"""Gunicorn — bound to loopback only; nginx faces the internet in Docker."""

import os

bind = os.getenv("GUNICORN_BIND", "127.0.0.1:8001")
workers = int(os.getenv("GUNICORN_WORKERS", "2"))
threads = int(os.getenv("GUNICORN_THREADS", "1"))
timeout = int(os.getenv("GUNICORN_TIMEOUT", "120"))
keepalive = 5

# Hardening
limit_request_line = 4094
limit_request_fields = 100
limit_request_field_size = 8190
forwarded_allow_ips = os.getenv("GUNICORN_FORWARDED_ALLOW_IPS", "127.0.0.1")
proxy_allow_ips = forwarded_allow_ips
accesslog = "-"
errorlog = "-"
capture_output = True
