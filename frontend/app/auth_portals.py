"""Sign-in portal definitions (customer, branch staff, administration)."""

from __future__ import annotations

from dataclasses import dataclass
from typing import FrozenSet

from flask import url_for

STAFF_PORTAL_ROLES: FrozenSet[str] = frozenset({"teller", "manager"})
ADMIN_PORTAL_ROLES: FrozenSet[str] = frozenset({"admin"})
CUSTOMER_PORTAL_ROLES: FrozenSet[str] = frozenset({"customer"})


@dataclass(frozen=True)
class LoginPortal:
    key: str
    endpoint: str
    allowed_roles: FrozenSet[str]
    page_title: str
    heading: str
    subtitle: str
    panel_heading: str
    panel_intro: str
    panel_bullets: tuple[str, ...]
    panel_theme: str  # css modifier: customer | staff | admin
    submit_label: str
    wrong_role_message: str


PORTALS: dict[str, LoginPortal] = {
    "customer": LoginPortal(
        key="customer",
        endpoint="auth.login",
        allowed_roles=CUSTOMER_PORTAL_ROLES,
        page_title="Customer sign in",
        heading="Sign in",
        subtitle="Personal online banking",
        panel_heading="Your accounts, securely managed",
        panel_intro="View balances, transfer funds, and download statements.",
        panel_bullets=(
            "Encrypted sign-in and session protection",
            "Real-time balances and transaction history",
            "Register once, then open accounts at any branch",
        ),
        panel_theme="customer",
        submit_label="Sign in to online banking",
        wrong_role_message=(
            "This account is not a personal banking customer. "
            "Branch staff should use staff sign-in; system administrators should use administration sign-in."
        ),
    ),
    "staff": LoginPortal(
        key="staff",
        endpoint="auth.staff_login",
        allowed_roles=STAFF_PORTAL_ROLES,
        page_title="Staff sign in",
        heading="Branch sign in",
        subtitle="Teller and manager portal",
        panel_heading="Branch operations",
        panel_intro="Process deposits, withdrawals, and account servicing for customers.",
        panel_bullets=(
            "Open and service customer accounts",
            "Cash deposit and withdrawal",
            "Manager tools for freeze and close",
        ),
        panel_theme="staff",
        submit_label="Sign in to branch portal",
        wrong_role_message=(
            "This account cannot use the branch portal. "
            "Customers should use personal sign-in; system administrators should use administration sign-in."
        ),
    ),
    "admin": LoginPortal(
        key="admin",
        endpoint="auth.admin_login",
        allowed_roles=ADMIN_PORTAL_ROLES,
        page_title="Administration sign in",
        heading="Administration sign in",
        subtitle="System administration",
        panel_heading="Platform administration",
        panel_intro="Assign roles, review audit logs, and manage system access.",
        panel_bullets=(
            "Role assignment for tellers and managers",
            "Security and account audit trail",
            "Restricted to admin accounts only",
        ),
        panel_theme="admin",
        submit_label="Sign in to administration",
        wrong_role_message=(
            "This account is not an administrator. "
            "Customers should use personal sign-in; branch staff should use staff sign-in."
        ),
    ),
}


def portal_for_role(role: str | None) -> str:
    if role in ADMIN_PORTAL_ROLES:
        return "admin"
    if role in STAFF_PORTAL_ROLES:
        return "staff"
    return "customer"


def login_url_for_portal(portal_key: str) -> str:
    portal = PORTALS[portal_key]
    return url_for(portal.endpoint)


def login_url_for_role(role: str | None) -> str:
    return login_url_for_portal(portal_for_role(role))


def other_portals(current_key: str) -> list[LoginPortal]:
    return [p for key, p in PORTALS.items() if key != current_key]
