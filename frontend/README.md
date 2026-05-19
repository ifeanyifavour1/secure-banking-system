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

4. Open **Register**, create a user, then **Sign in**. The dashboard shows JWT claims and lets you test **Refresh access token**.

Set `API_BASE_URL` and `FLASK_SECRET_KEY` in the project root `.env` if needed.
