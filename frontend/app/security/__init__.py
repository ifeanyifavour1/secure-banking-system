# Security package — HTTP security hardening for the Flask frontend
#
# This package contains:
# - headers.py: Flask-Talisman configuration for HSTS, CSP, X-Frame-Options,
#               X-Content-Type-Options, and HTTPS enforcement
#
# The init_security(app) function from headers.py should be called
# during app factory setup in app/__init__.py
