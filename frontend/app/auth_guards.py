"""Login redirects that match each portal (customer, staff, admin)."""

from __future__ import annotations

from functools import wraps

from flask import flash, redirect, session, url_for

def login_required(view):
    @wraps(view)
    def wrapped(*args, **kwargs):
        if not session.get("access_token"):
            flash("Please sign in first.", "warning")
            return redirect(url_for("auth.login"))
        return view(*args, **kwargs)

    return wrapped
