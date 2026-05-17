# Secure Banking System

Secure online banking platform with branch protection, cryptography, and network security controls.

**Team:** Blessed, Favour, Amodi
**University:** National Research Tomsk State University
**Course:** Introduction to Cybersecurity (BSc Software Engineering, Year 2)

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     PUBLIC ZONE                             │
│                 (Internet / Users)                          │
│                   HTTPS / TLS 1.3                           │
└───────────────────────┬─────────────────────────────────────┘
                        │  External Firewall + WAF
┌───────────────────────▼─────────────────────────────────────┐
│                        DMZ                                  │
│              Python Flask Frontend                          │
│         (HSTS, CSRF protection, input validation)           │
└───────────────────────┬─────────────────────────────────────┘
                        │  Internal Firewall
┌───────────────────────▼─────────────────────────────────────┐
│                PRIVATE APPLICATION ZONE                     │
│            C# .NET Core Web API (Backend)                   │
│     (JWT auth, RBAC, audit logging, business logic)         │
└───────────────────────┬─────────────────────────────────────┘
                        │  SQL Firewall
┌───────────────────────▼─────────────────────────────────────┐
│                RESTRICTED DATA ZONE                         │
│           PostgreSQL on Neon (free tier)                     │
│      (7 tables, double-entry bookkeeping, audit log)        │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```
coding/
├── .github/                    # GitHub branch protection & PR templates
├── backend/                    # C# .NET Core Web API
│   └── BankingApi/
│       ├── Auth/               # JWT token generation & validation
│       ├── Controllers/        # API endpoints (Auth, Accounts, Transactions, Audit)
│       ├── Data/               # EF Core DbContext (maps to PostgreSQL)
│       ├── DTOs/               # Data transfer objects
│       ├── Middleware/         # Audit logging middleware
│       ├── Models/             # Entity models matching banking_schema.sql
│       ├── Services/           # Business logic (transfers, limits)
│       └── Validators/         # Input validation
├── frontend/                   # Python Flask UI
│   └── app/
│       ├── routes/             # Auth, Dashboard, Transactions blueprints
│       ├── security/           # HSTS headers via Flask-Talisman
│       ├── validation/         # WTForms for server-side validation
│       ├── templates/          # Jinja2 HTML templates
│       └── static/             # CSS, JS assets
├── database/
│   ├── migrations/             # SQL DDL scripts (001_initial_schema.sql)
│   └── seeds/                  # Seed data (transaction states)
├── docs/
│   ├── threat-model/           # STRIDE analysis artifacts
│   └── architecture/           # Network diagrams, design docs
├── .env                        # Database connection string (not committed)
└── .gitignore
```

## Security Controls

| Control | Implementation |
|---------|---------------|
| Authentication | JWT + TOTP MFA |
| Authorization | RBAC (customer, teller, manager, admin) |
| Password storage | Bcrypt/Argon2 with per-user salt |
| Transport security | TLS 1.3 + HSTS |
| Audit trail | Immutable audit_log with JSONB snapshots |
| Fraud prevention | Account limits (daily/monthly caps) |
| Financial integrity | Double-entry bookkeeping |
| Input validation | Server-side (WTForms + .NET validators) |

## Database

Hosted on **Neon** (PostgreSQL free tier). Schema: `database/migrations/001_initial_schema.sql`

## Getting Started

### Backend (.NET 8)
```bash
cd backend/BankingApi
dotnet restore
dotnet run
```

### Frontend (Python Flask)
```bash
cd frontend
pip install -r requirements.txt
python run.py
```

# secure-banking-system
Secure banking system with branch protection, cryptography, and network security controls - Final Project 2026
