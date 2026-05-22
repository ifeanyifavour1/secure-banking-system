from flask import Blueprint, flash, redirect, render_template, session, url_for

from app.api_errors import handle_api_error
from app.auth_portals import PORTALS, login_url_for_portal, other_portals
from app.services.backend_api import BackendApiError
from app.services.backend_api import login as api_login
from app.services.backend_api import register as api_register
from app.utils import decode_jwt_payload
from app.security.limiter import AUTH_LIMIT, AUTH_REGISTER_LIMIT, limiter
from app.validation.forms import AdminLoginForm, LoginForm, RegisterForm, StaffLoginForm

auth_bp = Blueprint("auth", __name__)


def _store_auth_tokens(token_payload: dict) -> str | None:
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
    return role


def _role_allowed_for_portal(role: str | None, portal_key: str) -> bool:
    if not role:
        return False
    return role in PORTALS[portal_key].allowed_roles


def _clear_auth_session() -> None:
    for key in (
        "access_token",
        "refresh_token",
        "expires_at",
        "user_id",
        "email",
        "role",
        "login_portal",
    ):
        session.pop(key, None)


def _handle_portal_login(portal_key: str, form: LoginForm):
    portal = PORTALS[portal_key]

    if session.get("access_token"):
        return redirect(url_for("dashboard.index"))

    if form.validate_on_submit():
        try:
            token_payload = api_login(
                email=form.email.data.strip(),
                password=form.password.data,
                totp_code=form.totp_code.data.strip() if form.totp_code.data else None,
            )
            role = _store_auth_tokens(token_payload)
            if not _role_allowed_for_portal(role, portal_key):
                _clear_auth_session()
                flash(portal.wrong_role_message, "warning")
            else:
                session["login_portal"] = portal_key
                flash("Signed in successfully.", "success")
                return redirect(url_for("dashboard.index"))
        except BackendApiError as exc:
            handle_api_error(exc)

    return render_template(
        "auth/login_portal.html",
        form=form,
        portal=portal,
        other_portals=other_portals(portal_key),
        show_register_link=portal_key == "customer",
    )


@auth_bp.route("/")
def portal_hub():
    if session.get("access_token"):
        return redirect(url_for("dashboard.index"))
    return render_template("auth/portal_hub.html", portals=PORTALS)


@auth_bp.route("/login", methods=["GET", "POST"])
@limiter.limit(AUTH_LIMIT)
def login():
    return _handle_portal_login("customer", LoginForm())


@auth_bp.route("/staff/login", methods=["GET", "POST"])
@limiter.limit(AUTH_LIMIT)
def staff_login():
    return _handle_portal_login("staff", StaffLoginForm())


@auth_bp.route("/admin/login", methods=["GET", "POST"])
@limiter.limit(AUTH_LIMIT)
def admin_login():
    return _handle_portal_login("admin", AdminLoginForm())


@auth_bp.route("/register", methods=["GET", "POST"])
@limiter.limit(AUTH_REGISTER_LIMIT)
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
            handle_api_error(exc)

    return render_template("auth/register.html", form=form)


@auth_bp.route("/logout")
def logout():
    portal_key = session.get("login_portal", "customer")
    session.clear()
    flash("Signed out.", "info")
    return redirect(login_url_for_portal(portal_key if portal_key in PORTALS else "customer"))
