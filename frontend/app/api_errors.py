"""Consistent flash + cache handling when the banking API is unreachable."""

from __future__ import annotations

from flask import flash

from app.api_status import invalidate_api_status_cache
from app.services.backend_api import BackendApiError


def handle_api_error(exc: BackendApiError, *, on_get: bool = False) -> None:
    """Surface API errors without duplicating the global offline banner on GET."""
    if exc.connection_error:
        invalidate_api_status_cache()
        if not on_get:
            flash(exc.message, "warning")
        return
    flash(exc.message, "danger")
