from __future__ import annotations

import requests
from flask import current_app


class BackendApiError(Exception):
    def __init__(self, message: str, status_code: int | None = None):
        super().__init__(message)
        self.message = message
        self.status_code = status_code


def _base_url() -> str:
    return current_app.config["API_BASE_URL"]


def _request_kwargs() -> dict:
    return {"timeout": 30, "verify": current_app.config.get("API_VERIFY_SSL", True)}


def _extract_error_message(response: requests.Response) -> str:
    try:
        payload = response.json()
    except ValueError:
        return response.text or "Request failed."

    if isinstance(payload, dict):
        if "message" in payload:
            return str(payload["message"])
        if "detail" in payload:
            detail = str(payload["detail"])
            return detail if len(detail) <= 300 else detail[:300] + "..."
        if "title" in payload:
            return str(payload["title"])
        if "errors" in payload:
            errors = payload["errors"]
            messages: list[str] = []
            if isinstance(errors, dict):
                for field_errors in errors.values():
                    if isinstance(field_errors, list):
                        messages.extend(str(item) for item in field_errors)
                    else:
                        messages.append(str(field_errors))
            return "; ".join(messages) if messages else "Validation failed."

    return "Request failed."


def login(email: str, password: str, totp_code: str | None = None) -> dict:
    payload: dict[str, str] = {
        "email": email,
        "password": password,
    }
    if totp_code:
        payload["totpCode"] = totp_code

    response = requests.post(
        f"{_base_url()}/api/auth/login",
        json=payload,
        **_request_kwargs(),
    )

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()


def register(user_data: dict) -> dict:
    response = requests.post(
        f"{_base_url()}/api/auth/register",
        json=user_data,
        timeout=30,
    )

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()


def refresh(refresh_token: str) -> dict:
    response = requests.post(
        f"{_base_url()}/api/auth/refresh",
        json={"refreshToken": refresh_token},
        **_request_kwargs(),
    )

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()
