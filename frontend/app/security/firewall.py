"""Application-layer edge controls (second line behind nginx on production)."""

from __future__ import annotations

import re
from collections.abc import Iterable

from flask import Flask, abort, request
from werkzeug.exceptions import RequestEntityTooLarge

# Common scanner / exploit paths — never served by this app
BLOCKED_PATH_PREFIXES: tuple[str, ...] = (
    "/.env",
    "/.git",
    "/.svn",
    "/.aws",
    "/wp-admin",
    "/wp-content",
    "/wp-includes",
    "/wordpress",
    "/phpmyadmin",
    "/pma",
    "/administrator",
    "/xmlrpc.php",
    "/cgi-bin",
    "/actuator",
    "/console",
    "/vendor/phpunit",
    "/_ignition",
    "/telescope",
    "/server-status",
    "/.ds_store",
)

BLOCKED_PATH_PATTERNS: tuple[re.Pattern[str], ...] = (
    re.compile(r"\.(php|phtml|asp|aspx|jsp|cgi|env|bak|sql|swp)$", re.I),
    re.compile(r"/(config|backup|dump|shell|upload)\.", re.I),
)

SUSPICIOUS_USER_AGENT = re.compile(
    r"(sqlmap|nikto|acunetix|masscan|nmap|dirbuster|gobuster|havij|zgrab)",
    re.I,
)

MAX_QUERY_STRING_LEN = 2048


def _parse_allowed_hosts(raw: str) -> frozenset[str]:
    hosts: set[str] = set()
    for part in raw.split(","):
        host = part.strip().lower()
        if not host:
            continue
        hosts.add(host.split(":")[0])
    return frozenset(hosts)


def init_firewall(app: Flask) -> None:
    if not app.config.get("FIREWALL_ENABLED", True):
        return

    allowed_hosts: frozenset[str] = app.config.get("ALLOWED_HOSTS", frozenset())
    enforce_host = bool(allowed_hosts) and not app.debug
    max_query = int(app.config.get("FIREWALL_MAX_QUERY_STRING", MAX_QUERY_STRING_LEN))
    block_bad_ua = app.config.get("FIREWALL_BLOCK_SUSPICIOUS_UA", True)
    blocked_prefixes = _merged_prefixes(app.config.get("FIREWALL_EXTRA_BLOCKED_PREFIXES"))

    @app.before_request
    def _edge_firewall() -> None:
        if not app.config.get("FIREWALL_ENABLED", True):
            return

        path = (request.path or "/").lower()
        for prefix in blocked_prefixes:
            if path == prefix or path.startswith(prefix + "/") or path.startswith(prefix):
                abort(403)

        for pattern in BLOCKED_PATH_PATTERNS:
            if pattern.search(path):
                abort(403)

        if enforce_host:
            host = (request.host or "").split(":")[0].lower()
            if host and host not in allowed_hosts:
                abort(403)

        if len(request.query_string) > max_query:
            abort(414)

        if block_bad_ua:
            ua = request.headers.get("User-Agent", "")
            if ua and SUSPICIOUS_USER_AGENT.search(ua):
                abort(403)

    @app.errorhandler(RequestEntityTooLarge)
    def _payload_too_large(_exc: RequestEntityTooLarge):
        return {"error": "Request body too large."}, 413

    @app.errorhandler(403)
    def _forbidden(_exc):
        if request.accept_mimetypes.best_match(["application/json", "text/html"]) == "application/json":
            return {"error": "Forbidden."}, 403
        return (
            "<!DOCTYPE html><title>Forbidden</title><p>Request blocked.</p>",
            403,
            {"Content-Type": "text/html; charset=utf-8"},
        ),
    )


def _merged_prefixes(extra: Iterable[str] | None) -> tuple[str, ...]:
    if not extra:
        return BLOCKED_PATH_PREFIXES
    return tuple(dict.fromkeys([*BLOCKED_PATH_PREFIXES, *extra]))
