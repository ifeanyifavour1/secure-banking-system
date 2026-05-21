# Deploy to Render (free tier)

Host the **Flask UI** and **.NET API** on Render; keep **PostgreSQL on Neon** (already set up).

## Architecture

```
Browser → banking-frontend.onrender.com (Flask)
              ↓ API_BASE_URL
         banking-api.onrender.com (.NET)
              ↓ ConnectionStrings__neondb
         Neon PostgreSQL
```

## 1. Prerequisites

- GitHub repo pushed: `ifeanyifavour1/secure-banking-system` (branch `blessed_dev` or `main`)
- [Neon](https://console.neon.tech) connection string (.NET format)
- [Render](https://render.com) account (free)

## 2. Deploy with Blueprint

1. Render Dashboard → **New** → **Blueprint**
2. Connect your GitHub repo
3. Set **Root directory** to `project` if the repo root is the parent folder; if the repo root *is* `project`, leave blank.
4. Render detects `render.yaml` and creates two services: `banking-api`, `banking-frontend`
5. When prompted, fill **secret** environment variables (same values as local `project/.env`):

| Variable | Service | Example |
|----------|---------|---------|
| `ConnectionStrings__neondb` | banking-api | `Host=ep-....neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true` |
| `Jwt__Secret` | banking-api | Your base64 JWT secret |
| `Admin__RoleAssignmentSecret` | banking-api & banking-frontend | Same local admin secret |
| `FLASK_SECRET_KEY` | banking-frontend | Long random string |

`API_BASE_URL` is wired automatically from the API’s public URL.

6. Click **Apply** and wait for both services to go **Live** (first deploy ~5–10 min).

7. Copy the **banking-frontend** URL (e.g. `https://banking-frontend-xxxx.onrender.com`) into the **banking-api** env var **`Frontend__Url`**, then redeploy the API (needed for CORS).

## 3. Manual deploy (without Blueprint)

### banking-api (Docker)

| Setting | Value |
|---------|--------|
| Root Directory | `backend/BankingApi` |
| Environment | Docker |
| Dockerfile | `Dockerfile` |
| Health Check | `/health/db` |

**Environment variables:** `ASPNETCORE_ENVIRONMENT=Production`, plus secrets above.  
**Frontend__Url:** `https://<your-frontend>.onrender.com`

### banking-frontend (Python)

| Setting | Value |
|---------|--------|
| Root Directory | `frontend` |
| Build | `pip install -r requirements.txt` |
| Start | `gunicorn --bind 0.0.0.0:$PORT --workers 2 --timeout 120 run:app` |
| Python version | 3.12 |

**Environment variables:** `FLASK_ENV=production`, `API_BASE_URL=https://<your-api>.onrender.com`, plus secrets above.

## 4. Verify before class

1. Open `https://<banking-api>/health/db` → `"database":"connected"`
2. Open `https://<banking-frontend>/` → sign-in page loads
3. Register or use demo user (run seeds on Neon if needed)
4. **Wake services:** free tier sleeps after ~15 min idle; open both URLs 1–2 min before presenting

## 5. Demo users (after SQL seeds on Neon)

Run on Neon SQL editor:

- `database/seeds/002_demo_staff.sql`
- `database/seeds/003_demo_customers.sql`

Or register via the UI. Demo emails: `alice@demo.bank`, `bob@demo.bank` (passwords in seed file comments).

## 6. Troubleshooting

| Issue | Fix |
|-------|-----|
| Login shows database error | Check `ConnectionStrings__neondb` on API service; test `/health/db` |
| CORS / API errors from UI | Set `Frontend__Url` on API to exact frontend URL (with `https://`) |
| 502 on first load | Free tier cold start — wait 30–60s and refresh |
| Build fails on API | Ensure Root Directory is `backend/BankingApi` |

## 7. Local vs Render

| | Local | Render |
|---|--------|--------|
| API | `http://localhost:5285` | `https://banking-api-....onrender.com` |
| Flask | `http://127.0.0.1:5000` | `https://banking-frontend-....onrender.com` |
| Config | `project/.env` | Render dashboard env vars |

Do **not** commit `.env` to Git.
