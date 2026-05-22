# Flask frontend

## Run (demo login)

1. Trust the ASP.NET dev certificate (once per machine):

   ```bash
   dotnet dev-certs https --trust
   ```

2. Start the API (from `backend/BankingApi`):

   **HTTP only (simplest):**
   ```bash
   dotnet run --launch-profile http
   ```
   API: http://localhost:5285

   **HTTPS + HTTP redirect (TLS):**
   ```bash
   dotnet run --launch-profile https
   ```
   API: https://localhost:7285 (HTTP on 5285 redirects to HTTPS)

   For HTTPS from Flask, set in project `.env`:
   ```
   API_BASE_URL=https://localhost:7285
   API_VERIFY_SSL=false
   ```
   (`API_VERIFY_SSL=false` is for local dev certs only.)

3. Install and run the frontend:

   ```bash
   cd frontend
   pip install -r requirements.txt
   python run.py
   ```

   UI: http://127.0.0.1:5000

4. Open **Register** (customers), or pick a sign-in portal:
   - **Online banking** — `/auth/login` (customers)
   - **Branch staff** — `/auth/staff/login` (teller, manager)
   - **Administration** — `/auth/admin/login` (admin only)

   The home page (`/`) lists all portals. Each portal rejects accounts with the wrong role (e.g. a teller cannot sign in on the customer form).

Set `API_BASE_URL` and `FLASK_SECRET_KEY` in the project root `.env` if needed.

## Security edge (production)

On Render, the frontend Docker image runs **nginx** in front of **Gunicorn** (loopback only). Flask adds a second layer: path blocking, host validation, **Flask-Limiter**, Talisman headers, and CSRF. See [SECURITY.md](SECURITY.md).

Local `python run.py` uses the Flask layers only (no nginx).
