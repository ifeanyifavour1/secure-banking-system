from flask import Flask, request, session
from flask_wtf.csrf import CSRFProtect
from werkzeug.middleware.proxy_fix import ProxyFix

from app.config import Config
from app.routes.admin import admin_bp
from app.routes.auth import auth_bp
from app.routes.dashboard import dashboard_bp
from app.routes.staff import staff_bp
from app.routes.transactions import transactions_bp

STAFF_ROLES = frozenset({"teller", "manager", "admin"})
from app.security import init_app_security


def create_app() -> Flask:
    app = Flask(__name__)
    app.config.from_object(Config)

    CSRFProtect(app)
    init_app_security(app)

    if not app.debug:
        app.wsgi_app = ProxyFix(app.wsgi_app, x_proto=1, x_host=1, x_for=1)

    @app.route("/health")
    def health():
        return "ok", 200

    app.register_blueprint(auth_bp, url_prefix="/auth")
    app.register_blueprint(dashboard_bp, url_prefix="/dashboard")
    app.register_blueprint(admin_bp, url_prefix="/admin")
    app.register_blueprint(staff_bp, url_prefix="/staff")
    app.register_blueprint(transactions_bp, url_prefix="/transactions")

    @app.context_processor
    def inject_nav_context():
        from app.api_status import get_api_status

        role = session.get("role")
        force_health = request.args.get("recheck_api") == "1"
        api_status = get_api_status(force=force_health)
        return {
            "is_admin": role == "admin",
            "is_staff": role in STAFF_ROLES,
            "is_manager": role in ("manager", "admin"),
            "api_available": api_status["available"],
            "api_status_message": api_status["message"],
        }

    @app.route("/")
    def index():
        from flask import redirect, session, url_for

        if session.get("access_token"):
            return redirect(url_for("dashboard.index"))
        return redirect(url_for("auth.portal_hub"))

    return app
