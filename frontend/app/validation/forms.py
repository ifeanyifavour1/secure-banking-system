# WTForms for server-side input validation
#
# What to implement here:
# - LoginForm: email (required, valid email), password (required)
# - RegisterForm: first_name, last_name, email, password (min 8 chars), national_id
# - TransferForm: dest_account (required), amount (required, > 0), description (max 500)
#
# All forms use CSRF protection via Flask-WTF
