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


class Config:
    ENV = os.getenv("FLASK_ENV", "development")
    DEBUG = os.getenv("FLASK_DEBUG", "").lower() in {"1", "true", "yes"} or ENV != "production"
    SECRET_KEY = os.getenv("FLASK_SECRET_KEY", "dev-only-change-me")
    API_BASE_URL = _api_base_url()
    API_VERIFY_SSL = os.getenv("API_VERIFY_SSL", "true").lower() in {"1", "true", "yes"}
    ADMIN_ROLE_ASSIGNMENT_SECRET = os.getenv("Admin__RoleAssignmentSecret", "")
    WTF_CSRF_ENABLED = True
