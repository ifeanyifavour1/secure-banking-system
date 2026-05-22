from functools import wraps

from flask import Blueprint, flash, redirect, render_template, session, url_for

from app.api_errors import handle_api_error
from app.services.backend_api import BackendApiError, list_accounts, refresh

dashboard_bp = Blueprint("dashboard", __name__)


def login_required(view):
    @wraps(view)
    def wrapped(*args, **kwargs):
        if not session.get("access_token"):
            flash("Please sign in first.", "warning")
            return redirect(url_for("auth.login"))
        return view(*args, **kwargs)

    return wrapped


@dashboard_bp.route("/")
@login_required
def index():
    accounts: list[dict] = []
    accounts_unavailable = False
    access_token = session.get("access_token")

    if access_token:
        try:
            accounts = list_accounts(access_token)
        except BackendApiError as exc:
            accounts_unavailable = True
            handle_api_error(exc, on_get=True)

    return render_template(
        "dashboard/index.html",
        email=session.get("email"),
        role=session.get("role"),
        user_id=session.get("user_id"),
        accounts=accounts,
        accounts_unavailable=accounts_unavailable,
    )


@dashboard_bp.route("/refresh-token", methods=["POST"])
@login_required
def refresh_token():
    refresh_token_value = session.get("refresh_token")
    if not refresh_token_value:
        flash("No refresh token in session.", "danger")
        return redirect(url_for("dashboard.index"))

    try:
        token_payload = refresh(refresh_token_value)
        session["access_token"] = token_payload.get("accessToken")
        session["refresh_token"] = token_payload.get("refreshToken")
        session["expires_at"] = token_payload.get("expiresAt")
        flash("Access token refreshed.", "success")
    except BackendApiError as exc:
        handle_api_error(exc)

    return redirect(url_for("dashboard.index"))
