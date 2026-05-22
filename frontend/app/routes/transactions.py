from decimal import Decimal, InvalidOperation
from functools import wraps

from flask import Blueprint, flash, redirect, render_template, request, session, url_for

from app.api_errors import handle_api_error
from app.services.backend_api import (
    BackendApiError,
    get_transaction_history,
    list_accounts,
    lookup_account,
)
from app.services.backend_api import transfer as api_transfer
from app.validation.forms import TransferForm

transactions_bp = Blueprint("transactions", __name__)


def login_required(view):
    @wraps(view)
    def wrapped(*args, **kwargs):
        if not session.get("access_token"):
            flash("Please sign in first.", "warning")
            return redirect(url_for("auth.login"))
        return view(*args, **kwargs)

    return wrapped


def _active_account_choices(accounts: list[dict]) -> list[tuple[str, str]]:
    choices: list[tuple[str, str]] = []
    for account in accounts:
        if account.get("status") != "active":
            continue
        label = (
            f"{account.get('accountNumber')} · {account.get('accountType')} · "
            f"{account.get('availableBalance', account.get('balance')):.2f} {account.get('currency')}"
        )
        choices.append((account["accountId"], label))
    return choices


def _resolve_dest_account_id(form: TransferForm, access_token: str) -> str | None:
    if form.dest_type.data == "mine":
        if not form.dest_account_id.data:
            return None
        return form.dest_account_id.data

    number = (form.dest_account_number.data or "").strip()
    if not number:
        return None

    lookup = lookup_account(number, access_token)
    if lookup.get("status") != "active":
        raise BackendApiError("Destination account is not active.")
    return lookup["accountId"]


@transactions_bp.route("/transfer", methods=["GET", "POST"])
@login_required
def transfer():
    access_token = session.get("access_token")
    form = TransferForm()

    accounts_load_failed = False
    try:
        accounts = list_accounts(access_token) if access_token else []
    except BackendApiError as exc:
        accounts = []
        accounts_load_failed = True
        handle_api_error(exc, on_get=True)

    choices = _active_account_choices(accounts)
    form.source_account_id.choices = choices if choices else [("", "No active accounts")]
    form.dest_account_id.choices = choices if choices else [("", "No active accounts")]

    if accounts_load_failed or not choices:
        return render_template(
            "transactions/transfer.html",
            form=form,
            has_accounts=False,
            accounts_load_failed=accounts_load_failed,
        )

    if form.validate_on_submit():
        try:
            dest_id = _resolve_dest_account_id(form, access_token)
        except BackendApiError as exc:
            handle_api_error(exc)
            return render_template(
                "transactions/transfer.html",
                form=form,
                has_accounts=True,
                accounts_load_failed=False,
            )

        if not dest_id:
            if form.dest_type.data == "other":
                flash("Enter the recipient's account number.", "danger")
            else:
                flash("Select a destination account.", "danger")
            return render_template(
                "transactions/transfer.html",
                form=form,
                has_accounts=True,
            )

        if form.source_account_id.data == dest_id:
            flash("Source and destination must be different accounts.", "danger")
            return render_template(
                "transactions/transfer.html",
                form=form,
                has_accounts=True,
            )

        try:
            amount = float(Decimal(form.amount.data.strip()))
        except (InvalidOperation, ValueError):
            flash("Enter a valid amount.", "danger")
            return render_template(
                "transactions/transfer.html",
                form=form,
                has_accounts=True,
            )

        if amount <= 0:
            flash("Amount must be greater than zero.", "danger")
            return render_template(
                "transactions/transfer.html",
                form=form,
                has_accounts=True,
            )

        try:
            result = api_transfer(
                form.source_account_id.data,
                dest_id,
                amount,
                access_token,
                form.description.data.strip() if form.description.data else None,
            )
            flash(
                f"Transfer completed. Reference {result.get('referenceNumber')} · "
                f"{result.get('amount')} {result.get('currency')} · {result.get('state')}.",
                "success",
            )
            return redirect(url_for("dashboard.index"))
        except BackendApiError as exc:
            handle_api_error(exc)

    return render_template(
        "transactions/transfer.html",
        form=form,
        has_accounts=True,
        accounts_load_failed=False,
    )


@transactions_bp.route("/history")
@transactions_bp.route("/history/<account_id>")
@login_required
def history(account_id: str | None = None):
    access_token = session.get("access_token")
    page = max(1, int(request.args.get("page", 1) or 1))

    accounts: list[dict] = []
    accounts_load_failed = False
    try:
        accounts = list_accounts(access_token)
    except BackendApiError as exc:
        accounts_load_failed = True
        handle_api_error(exc, on_get=True)
        if not account_id:
            return render_template(
                "transactions/history_select.html",
                accounts=[],
                accounts_load_failed=True,
            )
        return redirect(url_for("dashboard.index"))

    if not account_id:
        active = [a for a in accounts if a.get("status") == "active"]
        if len(active) == 1:
            return redirect(url_for("transactions.history", account_id=active[0]["accountId"]))
        return render_template("transactions/history_select.html", accounts=accounts)

    try:
        data = get_transaction_history(account_id, access_token, page=page)
    except BackendApiError as exc:
        handle_api_error(exc, on_get=not account_id)
        return redirect(url_for("dashboard.index"))

    account = next((a for a in accounts if a.get("accountId") == account_id), None)
    total = data.get("totalCount", 0)
    page_size = data.get("pageSize", 20)
    total_pages = max(1, (total + page_size - 1) // page_size)

    return render_template(
        "transactions/history.html",
        account=account,
        account_id=account_id,
        transactions=data.get("transactions", []),
        page=data.get("page", page),
        page_size=page_size,
        total_count=total,
        total_pages=total_pages,
    )
