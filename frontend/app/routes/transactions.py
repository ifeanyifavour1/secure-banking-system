from flask import Blueprint, flash, redirect, render_template, session, url_for

transactions_bp = Blueprint("transactions", __name__)


@transactions_bp.route("/transfer")
def transfer():
    if not session.get("access_token"):
        flash("Please sign in first.", "warning")
        return redirect(url_for("auth.login"))

    return render_template(
        "transactions/transfer.html",
        message="Transfer UI will call the API using the stored JWT.",
    )
