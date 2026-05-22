"""Rate limiting — complements nginx limit_req in production."""

from __future__ import annotations

from flask import Flask, request
from flask_limiter import Limiter
from flask_limiter.util import get_remote_address

limiter = Limiter(
    key_func=get_remote_address,
    storage_uri="memory://",
    strategy="fixed-window",
    headers_enabled=True,
)


def _client_ip() -> str:
    """Prefer the first X-Forwarded-For hop when behind nginx / Render."""
    forwarded = request.headers.get("X-Forwarded-For", "").strip()
    if forwarded:
        return forwarded.split(",")[0].strip()
    return get_remote_address() or "127.0.0.1"


def init_limiter(app: Flask) -> None:
    if not app.config.get("RATELIMIT_ENABLED", True):
        limiter.enabled = False
        return

    limiter.init_app(app)
    limiter.key_func = _client_ip  # type: ignore[method-assign]

    @limiter.request_filter
    def _exempt_health_and_static() -> bool:
        if request.endpoint == "static":
            return True
        if request.endpoint == "health":
            return True
        return False


AUTH_LIMIT = "8 per minute; 40 per hour"
AUTH_REGISTER_LIMIT = "5 per minute; 20 per hour"
SENSITIVE_POST_LIMIT = "30 per minute; 150 per hour"
