from flask import Flask, session
from flask_wtf.csrf import CSRFProtect

from app.config import Config
from app.routes.admin import admin_bp
from app.routes.auth import auth_bp
from app.routes.dashboard import dashboard_bp
from app.routes.staff import staff_bp
from app.routes.transactions import transactions_bp

STAFF_ROLES = frozenset({"teller", "manager", "admin"})
from app.security.headers import init_security


def create_app() -> Flask:
    app = Flask(__name__)
    app.config.from_object(Config)

    CSRFProtect(app)
    init_security(app)

    app.register_blueprint(auth_bp, url_prefix="/auth")
    app.register_blueprint(dashboard_bp, url_prefix="/dashboard")
    app.register_blueprint(admin_bp, url_prefix="/admin")
    app.register_blueprint(staff_bp, url_prefix="/staff")
    app.register_blueprint(transactions_bp, url_prefix="/transactions")

    @app.context_processor
    def inject_nav_context():
        role = session.get("role")
        return {
            "is_admin": role == "admin",
            "is_staff": role in STAFF_ROLES,
            "is_manager": role in ("manager", "admin"),
        }

    @app.route("/")
    def index():
        from flask import redirect, session, url_for

        if session.get("access_token"):
            return redirect(url_for("dashboard.index"))
        return redirect(url_for("auth.login"))

    return app
