from flask import Flask
from flask_talisman import Talisman


def init_security(app: Flask) -> None:
    force_https = not app.debug
    Talisman(
        app,
        force_https=force_https,
        strict_transport_security=force_https,
        session_cookie_secure=force_https,
        frame_options="DENY",
        referrer_policy="strict-origin-when-cross-origin",
        content_security_policy={
            "default-src": "'self'",
            "style-src": ["'self'", "'unsafe-inline'", "https://fonts.googleapis.com"],
            "font-src": ["'self'", "https://fonts.gstatic.com"],
            "script-src": "'self'",
            "img-src": "'self' data:",
            "frame-ancestors": "'none'",
            "base-uri": "'self'",
            "form-action": "'self'",
        },
    )
