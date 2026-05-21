from functools import wraps

from flask import Blueprint, flash, redirect, render_template, request, session, url_for

from app.services.backend_api import BackendApiError, get_audit_logs, set_user_role
from app.validation.forms import AssignRoleForm

admin_bp = Blueprint("admin", __name__)


def admin_required(view):
    @wraps(view)
    def wrapped(*args, **kwargs):
        if not session.get("access_token"):
            flash("Please sign in first.", "warning")
            return redirect(url_for("auth.login"))
        if session.get("role") != "admin":
            flash("Admin access required.", "danger")
            return redirect(url_for("dashboard.index"))
        return view(*args, **kwargs)

    return wrapped


@admin_bp.route("/")
@admin_required
def index():
    form = AssignRoleForm()
    return render_template("admin/index.html", form=form)


@admin_bp.route("/assign-role", methods=["POST"])
@admin_required
def assign_role():
    form = AssignRoleForm()
    if not form.validate_on_submit():
        flash("Please fix the form errors.", "danger")
        return redirect(url_for("admin.index"))

    access_token = session.get("access_token")
    if not access_token:
        flash("Please sign in again.", "warning")
        return redirect(url_for("auth.login"))

    role = (form.role.data or "").strip().lower()
    if role == "admin":
        flash("The admin role cannot be assigned through the application.", "danger")
        return redirect(url_for("admin.index"))

    try:
        result = set_user_role(
            form.email.data.strip(),
            role,
            access_token,
        )
        flash(
            f"Role updated: {result.get('email')} is now {result.get('newRole')} "
            f"(was {result.get('previousRole')}).",
            "success",
        )
    except BackendApiError as exc:
        flash(exc.message, "danger")

    return redirect(url_for("admin.index"))


@admin_bp.route("/audit")
@admin_required
def audit_log():
    access_token = session.get("access_token")
    page = max(1, int(request.args.get("page", 1) or 1))

    try:
        data = get_audit_logs(access_token, page=page)
    except BackendApiError as exc:
        flash(exc.message, "danger")
        return redirect(url_for("admin.index"))

    total = data.get("totalCount", 0)
    page_size = data.get("pageSize", 50)
    total_pages = max(1, (total + page_size - 1) // page_size)

    return render_template(
        "admin/audit.html",
        entries=data.get("entries", []),
        page=data.get("page", page),
        total_pages=total_pages,
        total_count=total,
    )
