import os
from pathlib import Path

from dotenv import load_dotenv

PROJECT_ROOT = Path(__file__).resolve().parents[2]
load_dotenv(PROJECT_ROOT / ".env")


class Config:
    SECRET_KEY = os.getenv("FLASK_SECRET_KEY", "dev-only-change-me")
    API_BASE_URL = os.getenv("API_BASE_URL", "http://localhost:5285").rstrip("/")
    API_VERIFY_SSL = os.getenv("API_VERIFY_SSL", "true").lower() in {"1", "true", "yes"}
    WTF_CSRF_ENABLED = True
