import uuid

from flask_wtf import FlaskForm
from wtforms import DateField, PasswordField, SelectField, StringField, SubmitField
from wtforms.validators import DataRequired, Email, Length, Optional, Regexp, ValidationError


def _uuid_validator(form, field):
    try:
        uuid.UUID(str(field.data).strip())
    except ValueError as exc:
        raise ValidationError("Enter a valid user ID (UUID from the customer dashboard).") from exc


class LoginForm(FlaskForm):
    email = StringField("Email", validators=[DataRequired(), Email()])
    password = PasswordField("Password", validators=[DataRequired()])
    totp_code = StringField(
        "MFA code",
        validators=[Optional(), Length(min=6, max=6)],
        description="Required only if MFA is enabled on your account.",
    )
    submit = SubmitField("Sign in to online banking")


class StaffLoginForm(LoginForm):
    submit = SubmitField("Sign in to branch portal")


class AdminLoginForm(LoginForm):
    submit = SubmitField("Sign in to administration")


class RegisterForm(FlaskForm):
    first_name = StringField(
        "First name",
        validators=[
            DataRequired(),
            Length(min=2, max=50),
            Regexp(r"^[a-zA-Z]+$", message="Letters only."),
        ],
    )
    last_name = StringField(
        "Last name",
        validators=[
            DataRequired(),
            Length(min=2, max=50),
            Regexp(r"^[a-zA-Z]+$", message="Letters only."),
        ],
    )
    email = StringField("Email", validators=[DataRequired(), Email()])
    password = PasswordField(
        "Password",
        validators=[
            DataRequired(),
            Length(min=8, max=100),
            Regexp(
                r"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
                message="Must include upper, lower, digit, and special character.",
            ),
        ],
    )
    national_id = StringField("National ID", validators=[DataRequired(), Length(max=50)])
    date_of_birth = DateField("Date of birth", validators=[DataRequired()], format="%Y-%m-%d")
    phone_number = StringField(
        "Phone",
        validators=[
            DataRequired(),
            Regexp(
                r"^\+7\d{10}$",
                message="Phone must start with +7 and contain 10 digits after it.",
            ),
        ],
    )
    address_line1 = StringField(
        "Address line 1",
        validators=[DataRequired(), Regexp(r"^[a-zA-Z0-9\s]+$")],
    )
    address_line2 = StringField(
        "Address line 2 (optional)",
        validators=[Optional(), Length(max=100)],
    )
    city = StringField("City", validators=[DataRequired(), Length(max=50)])
    country = StringField("Country", validators=[DataRequired(), Length(max=56)])
    postal_code = StringField("Postal code", validators=[DataRequired(), Length(max=12)])
    submit = SubmitField("Create account")


class AssignRoleForm(FlaskForm):
    email = StringField("User email", validators=[DataRequired(), Email()])
    role = SelectField(
        "Role",
        choices=[
            ("customer", "customer"),
            ("teller", "teller"),
            ("manager", "manager"),
            ("admin", "admin"),
        ],
        default="teller",
        validators=[DataRequired()],
    )
    submit = SubmitField("Update role")


class OpenAccountForm(FlaskForm):
    user_id = StringField(
        "Customer user ID",
        validators=[DataRequired(), _uuid_validator],
        description="UUID shown on the customer's dashboard.",
    )
    account_type = SelectField(
        "Account type",
        choices=[
            ("checking", "checking"),
            ("savings", "savings"),
            ("fixed_deposit", "fixed_deposit"),
            ("loan", "loan"),
        ],
        default="checking",
        validators=[DataRequired()],
    )
    currency = StringField(
        "Currency",
        default="USD",
        validators=[
            DataRequired(),
            Length(min=3, max=3),
            Regexp(r"^[A-Z]{3}$", message="Use a 3-letter code, e.g. USD."),
        ],
    )
    submit = SubmitField("Open account")


class TransferForm(FlaskForm):
    source_account_id = SelectField(
        "From account",
        choices=[],
        validators=[DataRequired()],
    )
    dest_type = SelectField(
        "Destination",
        choices=[
            ("mine", "My other account"),
            ("other", "Another person's account"),
        ],
        default="other",
        validators=[DataRequired()],
    )
    dest_account_id = SelectField(
        "To my account",
        choices=[],
        validators=[Optional()],
    )
    dest_account_number = StringField(
        "Recipient account number",
        validators=[Optional(), Length(min=5, max=20)],
        description="e.g. SB2603BOBCHK0001 — ask the recipient for their account number.",
    )
    amount = StringField(
        "Amount",
        validators=[
            DataRequired(),
            Regexp(r"^\d+(\.\d{1,2})?$", message="Enter a valid amount (e.g. 100.00)."),
        ],
    )
    description = StringField(
        "Description (optional)",
        validators=[Optional(), Length(max=500)],
    )
    submit = SubmitField("Transfer")
