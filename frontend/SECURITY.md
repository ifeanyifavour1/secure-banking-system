# Frontend security edge

Traffic reaches Flask in two stages on production (Docker on Render):

```
Internet → nginx (rate limits, size caps, probe blocking) → Gunicorn (127.0.0.1:8001) → Flask (firewall + limiter + Talisman + CSRF)
```

## nginx (first line)

| Control | Value |
|--------|--------|
| General traffic | ~12 req/s per IP (burst 30) |
| `/auth/*` | ~6 req/min per IP (burst 8) |
| Max body | 512 KB |
| Connection limit | 30 per IP |
| Blocked paths | `.env`, `.git`, WordPress/PHP probes, etc. |
| Gunicorn exposure | Loopback only — not on the public interface |

## Flask firewall (`app/security/firewall.py`)

- **Host header** validation in production (`ALLOWED_HOSTS` / `RENDER_EXTERNAL_HOSTNAME`)
- Blocks scanner paths and suspicious user agents
- Max query string length (2048 bytes)
- `MAX_CONTENT_LENGTH` 512 KB (413 if exceeded)

## Rate limiting (`Flask-Limiter`)

| Route | Limit |
|-------|--------|
| Default | 120/min, 600/hour per IP |
| Sign-in / register | 8/min, 40/hour (auth) · 5/min register |
| Refresh token POST | 30/min |

Storage is in-memory per worker (`RATELIMIT_STORAGE_URI=memory://`). For multi-instance production, set Redis:

```env
RATELIMIT_STORAGE_URI=redis://:password@host:6379/0
```

## Local development

`python run.py` skips nginx; firewall, limiter, Talisman, and CSRF still apply. Host checks are off when `DEBUG` is true.

## Environment variables

| Variable | Purpose |
|----------|---------|
| `ALLOWED_HOSTS` | Comma-separated hostnames (optional; Render hostname auto-added) |
| `FIREWALL_ENABLED` | `true` / `false` |
| `RATELIMIT_ENABLED` | `true` / `false` |
| `RATELIMIT_DEFAULT` | e.g. `120 per minute; 600 per hour` |
| `MAX_CONTENT_LENGTH` | Bytes (default 524288) |
