"""Cached API reachability for UI fallbacks (banner, disabled forms)."""

from __future__ import annotations

import time

from app.services.backend_api import check_api_health

_CACHE_TTL_SECONDS = 20
_cache: dict = {
    "at": 0.0,
    "available": None,
    "message": "",
}


def get_api_status(*, force: bool = False) -> dict[str, object]:
    now = time.monotonic()
    if (
        not force
        and _cache["available"] is not None
        and (now - _cache["at"]) < _CACHE_TTL_SECONDS
    ):
        return {
            "available": _cache["available"],
            "message": _cache["message"],
        }

    available, message = check_api_health()
    _cache["at"] = now
    _cache["available"] = available
    _cache["message"] = message
    return {"available": available, "message": message}


def invalidate_api_status_cache() -> None:
    _cache["available"] = None
    _cache["at"] = 0.0
