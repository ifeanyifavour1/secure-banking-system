import os
from pathlib import Path

from dotenv import load_dotenv

PROJECT_ROOT = Path(__file__).resolve().parents[2]
# override=True: project .env wins over stale shell vars (e.g. old https://localhost:7285)
load_dotenv(PROJECT_ROOT / ".env", override=True)


def _api_base_url() -> str:
    explicit = os.getenv("API_BASE_URL", "").strip()
    if explicit:
        return explicit.rstrip("/")

    host = os.getenv("API_HOST", "").strip()
    if host:
        return f"https://{host.removeprefix('https://').removeprefix('http://').rstrip('/')}"

    return "http://localhost:5285"


def _allowed_hosts() -> frozenset[str]:
    explicit = os.getenv("ALLOWED_HOSTS", "").strip()
    hosts: set[str] = set()
    if explicit:
        for part in explicit.split(","):
            h = part.strip().lower().split(":")[0]
            if h:
                hosts.add(h)
    render_host = os.getenv("RENDER_EXTERNAL_HOSTNAME", "").strip().lower()
    if render_host:
        hosts.add(render_host.split(":")[0])
    if not hosts and os.getenv("FLASK_ENV", "development") == "production":
        # Safe default for local gunicorn without Docker
        hosts.update({"127.0.0.1", "localhost"})
    return frozenset(hosts)


def _env_bool(name: str, default: bool = True) -> bool:
    raw = os.getenv(name)
    if raw is None:
        return default
    return raw.lower() in {"1", "true", "yes", "on"}


class Config:
    ENV = os.getenv("FLASK_ENV", "development")
    DEBUG = os.getenv("FLASK_DEBUG", "").lower() in {"1", "true", "yes"} or ENV != "production"
    SECRET_KEY = os.getenv("FLASK_SECRET_KEY", "dev-only-change-me")
    API_BASE_URL = _api_base_url()
    API_VERIFY_SSL = os.getenv("API_VERIFY_SSL", "true").lower() in {"1", "true", "yes"}
    ADMIN_ROLE_ASSIGNMENT_SECRET = os.getenv("Admin__RoleAssignmentSecret", "")
    WTF_CSRF_ENABLED = True

    # Edge firewall + rate limiting
    MAX_CONTENT_LENGTH = int(os.getenv("MAX_CONTENT_LENGTH", str(512 * 1024)))
    ALLOWED_HOSTS = _allowed_hosts()
    FIREWALL_ENABLED = _env_bool("FIREWALL_ENABLED", True)
    FIREWALL_BLOCK_SUSPICIOUS_UA = _env_bool("FIREWALL_BLOCK_SUSPICIOUS_UA", True)
    FIREWALL_MAX_QUERY_STRING = int(os.getenv("FIREWALL_MAX_QUERY_STRING", "2048"))
    RATELIMIT_ENABLED = _env_bool("RATELIMIT_ENABLED", True)
    RATELIMIT_DEFAULT = os.getenv("RATELIMIT_DEFAULT", "120 per minute; 600 per hour")
    RATELIMIT_STORAGE_URI = os.getenv("RATELIMIT_STORAGE_URI", "memory://")
    # Flask-Limiter reads these keys from app.config
    RATELIMIT_STORAGE_URL = RATELIMIT_STORAGE_URI
