from flask import Blueprint, flash, redirect, render_template, session, url_for

from app.services.backend_api import BackendApiError
from app.services.backend_api import login as api_login
from app.services.backend_api import register as api_register
from app.utils import decode_jwt_payload
from app.validation.forms import LoginForm, RegisterForm

auth_bp = Blueprint("auth", __name__)


def _store_auth_tokens(token_payload: dict) -> None:
    access_token = token_payload.get("accessToken", "")
    session["access_token"] = access_token
    session["refresh_token"] = token_payload.get("refreshToken")
    session["expires_at"] = token_payload.get("expiresAt")

    claims = decode_jwt_payload(access_token)
    session["user_id"] = claims.get("sub")
    session["email"] = (
        claims.get("email")
        or claims.get("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/email")
    )
    role = claims.get("role") or claims.get(
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    )
    if isinstance(role, list):
        role = role[0] if role else None
    session["role"] = role


@auth_bp.route("/login", methods=["GET", "POST"])
def login():
    if session.get("access_token"):
        return redirect(url_for("dashboard.index"))

    form = LoginForm()
    if form.validate_on_submit():
        try:
            token_payload = api_login(
                email=form.email.data.strip(),
                password=form.password.data,
                totp_code=form.totp_code.data.strip() if form.totp_code.data else None,
            )
            _store_auth_tokens(token_payload)
            flash("Signed in successfully.", "success")
            return redirect(url_for("dashboard.index"))
        except BackendApiError as exc:
            flash(exc.message, "danger")

    return render_template("auth/login.html", form=form)


@auth_bp.route("/register", methods=["GET", "POST"])
def register():
    if session.get("access_token"):
        return redirect(url_for("dashboard.index"))

    form = RegisterForm()
    if form.validate_on_submit():
        payload = {
            "firstName": form.first_name.data.strip(),
            "lastName": form.last_name.data.strip(),
            "email": form.email.data.strip(),
            "password": form.password.data,
            "nationalId": form.national_id.data.strip(),
            "dateOfBirth": form.date_of_birth.data.isoformat(),
            "phoneNumber": form.phone_number.data.strip(),
            "addressLine1": form.address_line1.data.strip(),
            "addressLine2": form.address_line2.data.strip() or None,
            "city": form.city.data.strip(),
            "country": form.country.data.strip(),
            "postalCode": form.postal_code.data.strip(),
        }

        try:
            api_register(payload)
            flash("Account created. You can sign in now.", "success")
            return redirect(url_for("auth.login"))
        except BackendApiError as exc:
            flash(exc.message, "danger")

    return render_template("auth/register.html", form=form)


@auth_bp.route("/logout")
def logout():
    session.clear()
    flash("Signed out.", "info")
    return redirect(url_for("auth.login"))
