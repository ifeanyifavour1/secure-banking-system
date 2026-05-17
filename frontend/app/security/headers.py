# Security headers configuration
#
# What to implement here:
# - Use Flask-Talisman to apply:
#   - HSTS (Strict-Transport-Security) with max-age=31536000
#   - Content-Security-Policy: default-src 'self'
#   - X-Content-Type-Options: nosniff
#   - X-Frame-Options: DENY
# - Set force_https=True in production (behind TLS terminator)
