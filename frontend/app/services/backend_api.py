from __future__ import annotations

from urllib.parse import quote

import requests
from flask import current_app
from requests.exceptions import ConnectionError as RequestsConnectionError
from requests.exceptions import RequestException
from requests.exceptions import Timeout as RequestsTimeout


class BackendApiError(Exception):
    def __init__(
        self,
        message: str,
        status_code: int | None = None,
        *,
        connection_error: bool = False,
    ):
        super().__init__(message)
        self.message = message
        self.status_code = status_code
        self.connection_error = connection_error


def _base_url() -> str:
    return current_app.config["API_BASE_URL"]


def _request_kwargs() -> dict:
    return {"timeout": 30, "verify": current_app.config.get("API_VERIFY_SSL", True)}


def _is_production() -> bool:
    try:
        return current_app.config.get("ENV") == "production"
    except RuntimeError:
        return False


def _connection_error_message() -> str:
    base = _base_url()
    if _is_production():
        return (
            f"The banking API at {base} is not responding. "
            "If you use Render’s free tier, the API may be waking up—wait about a minute and try again. "
            "If the problem persists, check the API service logs and environment variables in the Render dashboard."
        )
    return (
        f"Cannot reach the banking API at {base}. "
        "Start the API with: dotnet run --launch-profile http "
        "(from backend/BankingApi, listens on http://localhost:5285). "
        "Set API_BASE_URL=http://localhost:5285 in project/.env and restart Flask."
    )


def _timeout_error_message() -> str:
    base = _base_url()
    return (
        f"The banking API at {base} did not respond in time. "
        "The service may be starting up or under heavy load—try again in a moment."
    )


def _raise_connection_error(exc: Exception) -> None:
    raise BackendApiError(
        _connection_error_message(),
        connection_error=True,
    ) from exc


def _raise_timeout_error(exc: Exception) -> None:
    raise BackendApiError(
        _timeout_error_message(),
        connection_error=True,
    ) from exc


def check_api_health() -> tuple[bool, str]:
    """Lightweight liveness probe (no auth). Returns (ok, user-facing message if not ok)."""
    try:
        response = requests.get(
            f"{_base_url()}/health",
            timeout=4,
            verify=current_app.config.get("API_VERIFY_SSL", True),
        )
    except RequestsConnectionError:
        return False, _connection_error_message()
    except RequestsTimeout:
        return False, _timeout_error_message()
    except RequestException as exc:
        return False, f"API health check failed: {exc}"

    if response.status_code == 200:
        return True, ""

    return False, (
        f"The banking API at {_base_url()} returned HTTP {response.status_code}. "
        "Check that the API service is running and healthy."
    )


def _authorized_headers(access_token: str, *, include_admin_secret: bool = False) -> dict[str, str]:
    headers = {"Authorization": f"Bearer {access_token}"}
    if include_admin_secret:
        secret = current_app.config.get("ADMIN_ROLE_ASSIGNMENT_SECRET", "")
        if secret:
            headers["X-Admin-Secret"] = secret
    return headers


def _get(
    path: str,
    *,
    access_token: str,
    params: dict | None = None,
) -> requests.Response:
    kwargs = _request_kwargs()
    kwargs["headers"] = _authorized_headers(access_token)

    try:
        return requests.get(
            f"{_base_url()}{path}",
            params=params,
            **kwargs,
        )
    except RequestsConnectionError as exc:
        _raise_connection_error(exc)
    except RequestsTimeout as exc:
        _raise_timeout_error(exc)
    except RequestException as exc:
        raise BackendApiError(f"API request failed: {exc}") from exc


def _patch(path: str, *, access_token: str) -> requests.Response:
    kwargs = _request_kwargs()
    kwargs["headers"] = _authorized_headers(access_token)

    try:
        return requests.patch(f"{_base_url()}{path}", **kwargs)
    except RequestsConnectionError as exc:
        _raise_connection_error(exc)
    except RequestsTimeout as exc:
        _raise_timeout_error(exc)
    except RequestException as exc:
        raise BackendApiError(f"API request failed: {exc}") from exc


def _post(
    path: str,
    json: dict,
    *,
    access_token: str | None = None,
    include_admin_secret: bool = False,
) -> requests.Response:
    kwargs = _request_kwargs()
    if access_token:
        kwargs["headers"] = _authorized_headers(
            access_token,
            include_admin_secret=include_admin_secret,
        )

    try:
        return requests.post(f"{_base_url()}{path}", json=json, **kwargs)
    except RequestsConnectionError as exc:
        _raise_connection_error(exc)
    except RequestsTimeout as exc:
        _raise_timeout_error(exc)
    except RequestException as exc:
        raise BackendApiError(f"API request failed: {exc}") from exc


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

    response = _post("/api/auth/login", payload)

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()


def register(user_data: dict) -> dict:
    response = _post("/api/auth/register", user_data)

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()


def refresh(refresh_token: str) -> dict:
    response = _post("/api/auth/refresh", {"refreshToken": refresh_token})

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()


def set_user_role(email: str, role: str, access_token: str) -> dict:
    response = _post(
        "/api/internal/staff/role",
        {"email": email, "role": role},
        access_token=access_token,
        include_admin_secret=True,
    )

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()


def lookup_account(account_number: str, access_token: str) -> dict:
    number = account_number.strip()
    response = _get(
        f"/api/accounts/lookup/{quote(number, safe='')}",
        access_token=access_token,
    )

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()


def list_accounts(access_token: str, user_id: str | None = None) -> list[dict]:
    params = {"userId": user_id} if user_id else None
    response = _get("/api/accounts", access_token=access_token, params=params)

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    payload = response.json()
    return payload.get("accounts", [])


def create_account(
    user_id: str,
    account_type: str,
    currency: str,
    access_token: str,
) -> dict:
    response = _post(
        "/api/accounts",
        {
            "userId": user_id,
            "accountType": account_type,
            "currency": currency,
        },
        access_token=access_token,
    )

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()


def deposit(account_id: str, amount: float, access_token: str, description: str | None = None) -> dict:
    payload: dict = {"accountId": account_id, "amount": amount}
    if description:
        payload["description"] = description
    response = _post("/api/transactions/deposit", payload, access_token=access_token)
    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)
    return response.json()


def withdrawal(account_id: str, amount: float, access_token: str, description: str | None = None) -> dict:
    payload: dict = {"accountId": account_id, "amount": amount}
    if description:
        payload["description"] = description
    response = _post("/api/transactions/withdrawal", payload, access_token=access_token)
    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)
    return response.json()


def get_transaction_history(
    account_id: str,
    access_token: str,
    *,
    page: int = 1,
    page_size: int = 20,
) -> dict:
    response = _get(
        f"/api/transactions/history/{account_id}",
        access_token=access_token,
        params={"page": page, "pageSize": page_size},
    )
    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)
    return response.json()


def freeze_account(account_id: str, access_token: str) -> dict:
    response = _patch(f"/api/accounts/{account_id}/freeze", access_token=access_token)
    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)
    return response.json()


def close_account(account_id: str, access_token: str) -> dict:
    response = _patch(f"/api/accounts/{account_id}/close", access_token=access_token)
    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)
    return response.json()


def get_audit_logs(access_token: str, *, page: int = 1, page_size: int = 50) -> dict:
    response = _get(
        "/api/audit",
        access_token=access_token,
        params={"page": page, "pageSize": page_size},
    )
    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)
    return response.json()


def transfer(
    source_account_id: str,
    dest_account_id: str,
    amount: float,
    access_token: str,
    description: str | None = None,
) -> dict:
    payload: dict = {
        "sourceAccountId": source_account_id,
        "destAccountId": dest_account_id,
        "amount": amount,
    }
    if description:
        payload["description"] = description

    response = _post("/api/transactions/transfer", payload, access_token=access_token)

    if response.status_code >= 400:
        raise BackendApiError(_extract_error_message(response), response.status_code)

    return response.json()
