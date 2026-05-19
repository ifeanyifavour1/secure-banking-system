from flask_wtf import FlaskForm
from wtforms import DateField, PasswordField, StringField, SubmitField
from wtforms.validators import DataRequired, Email, Length, Optional, Regexp


class LoginForm(FlaskForm):
    email = StringField("Email", validators=[DataRequired(), Email()])
    password = PasswordField("Password", validators=[DataRequired()])
    totp_code = StringField(
        "MFA code",
        validators=[Optional(), Length(min=6, max=6)],
        description="Required only if MFA is enabled on your account.",
    )
    submit = SubmitField("Sign in")


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
