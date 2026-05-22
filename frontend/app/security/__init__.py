from flask import Flask

from app.security.firewall import init_firewall
from app.security.headers import init_security
from app.security.limiter import init_limiter


def init_app_security(app: Flask) -> None:
    """Headers (Talisman), edge firewall middleware, and Flask-Limiter."""
    init_security(app)
    init_firewall(app)
    init_limiter(app)
