from decimal import Decimal, InvalidOperation
from functools import wraps

from flask import Blueprint, flash, redirect, render_template, request, session, url_for

from app.services.backend_api import (
    BackendApiError,
    close_account,
    create_account,
    deposit,
    freeze_account,
    list_accounts,
    withdrawal,
)

MANAGER_ROLES = frozenset({"manager", "admin"})
from app.validation.forms import OpenAccountForm

staff_bp = Blueprint("staff", __name__)

STAFF_ROLES = frozenset({"teller", "manager", "admin"})


def staff_required(view):
    @wraps(view)
    def wrapped(*args, **kwargs):
        if not session.get("access_token"):
            flash("Please sign in first.", "warning")
            return redirect(url_for("auth.login"))
        if session.get("role") not in STAFF_ROLES:
            flash("Teller access or above is required.", "danger")
            return redirect(url_for("dashboard.index"))
        return view(*args, **kwargs)

    return wrapped


def _parse_amount(raw: str) -> float | None:
    try:
        value = float(Decimal(raw.strip()))
    except (InvalidOperation, ValueError):
        return None
    return value if value > 0 else None


@staff_bp.route("/open-account", methods=["GET", "POST"])
@staff_required
def open_account():
    form = OpenAccountForm()
    if form.validate_on_submit():
        access_token = session.get("access_token")
        try:
            account = create_account(
                str(form.user_id.data).strip(),
                form.account_type.data,
                form.currency.data.strip().upper(),
                access_token,
            )
            flash(
                f"Account opened: {account.get('accountNumber')} "
                f"({account.get('accountType')}, {account.get('currency')}).",
                "success",
            )
            return redirect(url_for("staff.open_account"))
        except BackendApiError as exc:
            flash(exc.message, "danger")

    return render_template("staff/open_account.html", form=form)


@staff_bp.route("/deposit", methods=["GET", "POST"])
@staff_required
def deposit_funds():
    access_token = session.get("access_token")
    accounts = []
    try:
        accounts = list_accounts(access_token)
    except BackendApiError as exc:
        flash(exc.message, "danger")

    if request.method == "POST":
        account_id = request.form.get("account_id", "").strip()
        amount = _parse_amount(request.form.get("amount", ""))
        description = request.form.get("description", "").strip() or None

        if not account_id or amount is None:
            flash("Select an account and enter a valid amount.", "danger")
        else:
            try:
                result = deposit(account_id, amount, access_token, description)
                flash(
                    f"Deposit completed. Reference {result.get('referenceNumber')} · "
                    f"{result.get('amount')} {result.get('currency')}.",
                    "success",
                )
                return redirect(url_for("staff.deposit_funds"))
            except BackendApiError as exc:
                flash(exc.message, "danger")

    return render_template("staff/deposit.html", accounts=accounts)


@staff_bp.route("/withdrawal", methods=["GET", "POST"])
@staff_required
def withdraw_funds():
    access_token = session.get("access_token")
    accounts = []
    try:
        accounts = list_accounts(access_token)
    except BackendApiError as exc:
        flash(exc.message, "danger")

    if request.method == "POST":
        account_id = request.form.get("account_id", "").strip()
        amount = _parse_amount(request.form.get("amount", ""))
        description = request.form.get("description", "").strip() or None

        if not account_id or amount is None:
            flash("Select an account and enter a valid amount.", "danger")
        else:
            try:
                result = withdrawal(account_id, amount, access_token, description)
                flash(
                    f"Withdrawal completed. Reference {result.get('referenceNumber')} · "
                    f"{result.get('amount')} {result.get('currency')}.",
                    "success",
                )
                return redirect(url_for("staff.withdraw_funds"))
            except BackendApiError as exc:
                flash(exc.message, "danger")

    return render_template("staff/withdrawal.html", accounts=accounts)


def manager_required(view):
    @wraps(view)
    def wrapped(*args, **kwargs):
        if not session.get("access_token"):
            flash("Please sign in first.", "warning")
            return redirect(url_for("auth.login"))
        if session.get("role") not in MANAGER_ROLES:
            flash("Manager access or above is required.", "danger")
            return redirect(url_for("dashboard.index"))
        return view(*args, **kwargs)

    return wrapped


@staff_bp.route("/accounts", methods=["GET", "POST"])
@manager_required
def manage_accounts():
    access_token = session.get("access_token")
    accounts = []
    try:
        accounts = list_accounts(access_token)
    except BackendApiError as exc:
        flash(exc.message, "danger")

    if request.method == "POST":
        account_id = request.form.get("account_id", "").strip()
        action = request.form.get("action", "").strip()

        if not account_id:
            flash("Select an account.", "danger")
        elif action == "freeze":
            try:
                result = freeze_account(account_id, access_token)
                flash(f"Account {result.get('accountNumber')} is now {result.get('status')}.", "success")
            except BackendApiError as exc:
                flash(exc.message, "danger")
        elif action == "close":
            try:
                result = close_account(account_id, access_token)
                flash(f"Account {result.get('accountNumber')} is now {result.get('status')}.", "success")
            except BackendApiError as exc:
                flash(exc.message, "danger")
        else:
            flash("Unknown action.", "danger")

        return redirect(url_for("staff.manage_accounts"))

    return render_template("staff/manage_accounts.html", accounts=accounts)
