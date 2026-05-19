from flask import Flask
from flask_talisman import Talisman


def init_security(app: Flask) -> None:
    force_https = not app.debug
    Talisman(
        app,
        force_https=force_https,
        strict_transport_security=force_https,
        session_cookie_secure=force_https,
        content_security_policy={
            "default-src": "'self'",
            "style-src": ["'self'", "'unsafe-inline'"],
            "script-src": "'self'",
        },
    )
