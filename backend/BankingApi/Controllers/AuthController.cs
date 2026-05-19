using System.Net;
using System.Text.Json;
using BankingApi.Auth;
using BankingApi.Data;
using BankingApi.DTOs;
using BankingApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private const int MaxFailedLoginAttempts = 5;
        private const int WorkFactor = 12;

        private readonly BankingDbContext _db;
        private readonly JwtTokenService _jwtTokenService;

        public AuthController(BankingDbContext db, JwtTokenService jwtTokenService)
        {
            _db = db;
            _jwtTokenService = jwtTokenService;
        }
        

        //Amodi , implement this endpoint
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var normalizedNationalId = request.NationalId.Trim();

            if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
            {
                return BadRequest(new { message = "Email is already registered." });
            }

            if (await _db.Users.AnyAsync(u => u.NationalId == normalizedNationalId))
            {
                return BadRequest(new { message = "National ID is already registered." });
            }

            var now = DateTime.UtcNow;

            var user = new User
            {
                UserId = Guid.NewGuid(),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber.Trim(),
                PasswordHash = PasswordHashing.HashPassword(request.Password, WorkFactor),
                PasswordSalt = Array.Empty<byte>(),
                DateOfBirth = request.DateOfBirth,
                NationalId = normalizedNationalId,
                AddressLine1 = request.AddressLine1.Trim(),
                AddressLine2 = string.IsNullOrWhiteSpace(request.AddressLine2) ? null : request.AddressLine2.Trim(),
                City = request.City.Trim(),
                Country = request.Country.Trim(),
                PostalCode = request.PostalCode.Trim(),
                Role = "customer",
                MfaEnabled = false,
                IsActive = true,
                IsLocked = false,
                FailedLoginCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Users.Add(user);
            LogUserRegistration(user, now);
            await _db.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Registration successful.",
                userId = user.UserId,
                email = user.Email
            });
        }

        //Favour, implement this endpoint
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

            if (user == null)
            {
                LogFailedLoginAttempt(normalizedEmail, DateTime.UtcNow);
                await _db.SaveChangesAsync();

                return Unauthorized(new { message = "Invalid email or password." });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new { message = "Account is disabled. Please contact support." });
            }

            if (user.IsLocked)
            {
                return Unauthorized(new { message = "Account is locked due to too many failed login attempts " });
            }

            var plainTextPassword = request.Password;
            bool isValid = PasswordHashing.Verify(plainTextPassword, user.PasswordHash);

            if (!isValid)
            {
                await HandleFailedLoginAsync(user, normalizedEmail, "invalid_password");
                return Unauthorized(new { message = "Invalid email or password." });
            }

            if (user.MfaEnabled)
            {
                if (string.IsNullOrWhiteSpace(request.TotpCode))
                {
                    return Unauthorized(new { message = "MFA code is required." });
                }

                if (!TotpValidator.Validate(user.MfaSecret, request.TotpCode))
                {
                    await HandleFailedLoginAsync(user, normalizedEmail, "invalid_mfa");
                    return Unauthorized(new { message = "Invalid email or password." });
                }
            }

            if (PasswordHashing.NeedsRehash(user.PasswordHash, WorkFactor))
            {
                user.PasswordHash = PasswordHashing.HashPassword(plainTextPassword, WorkFactor);
            }

            user.FailedLoginCount = 0;
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            var authResponse = _jwtTokenService.GenerateTokenPair(user);
            LogSuccessfulLogin(user, DateTime.UtcNow);
            await _db.SaveChangesAsync();

            return Ok(authResponse);
        }


        //Blessed, implement this endpoint
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var userId = _jwtTokenService.ValidateRefreshToken(request.RefreshToken);
            if (userId is null)
            {
                return Unauthorized(new { message = "Invalid or expired refresh token." });
            }

            var user = await _db.Users.FindAsync(userId.Value);
            if (user is null)
            {
                return Unauthorized(new { message = "Invalid or expired refresh token." });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new { message = "Account is disabled. Please contact support." });
            }

            if (user.IsLocked)
            {
                return Unauthorized(new { message = "Account is locked due to too many failed login attempts " });
            }

            user.UpdatedAt = DateTime.UtcNow;
            var authResponse = _jwtTokenService.GenerateTokenPair(user);
            await _db.SaveChangesAsync();

            return Ok(authResponse);
        }

        private void LogUserRegistration(User user, DateTime createdAt)
        {
            var auditEntry = new AuditLogEntry
            {
                EventType = "authentication",
                EntityType = "user",
                EntityId = user.UserId.ToString(),
                Action = "create",
                PerformedBy = user.UserId,
                IpAddress = GetClientIpAddress(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                AdditionalInfo = JsonSerializer.SerializeToDocument(new { user.Email, createdAt }),
                CreatedAt = createdAt
            };

            _db.AuditLogEntries.Add(auditEntry);
        }

        private void LogSuccessfulLogin(User user, DateTime loggedInAt)
        {
            var auditEntry = new AuditLogEntry
            {
                EventType = "authentication",
                EntityType = "user",
                EntityId = user.UserId.ToString(),
                Action = "login",
                PerformedBy = user.UserId,
                IpAddress = GetClientIpAddress(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                AdditionalInfo = JsonSerializer.SerializeToDocument(new { user.Email, loggedInAt }),
                CreatedAt = loggedInAt
            };

            _db.AuditLogEntries.Add(auditEntry);
        }

        private async Task HandleFailedLoginAsync(User user, string email, string reason)
        {
            LogFailedLoginAttempt(email, DateTime.UtcNow, user.UserId, reason);

            user.FailedLoginCount += 1;

            if (user.FailedLoginCount >= MaxFailedLoginAttempts)
            {
                user.IsLocked = true;
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        private void LogFailedLoginAttempt(
            string email,
            DateTime attemptedAt,
            Guid? userId = null,
            string? reason = null)
        {
            var auditEntry = new AuditLogEntry
            {
                EventType = "authentication",
                EntityType = "user",
                EntityId = userId?.ToString() ?? email,
                Action = "failed_login",
                PerformedBy = userId,
                IpAddress = GetClientIpAddress(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                AdditionalInfo = JsonSerializer.SerializeToDocument(new { email, attemptedAt, reason }),
                CreatedAt = attemptedAt
            };

            _db.AuditLogEntries.Add(auditEntry);
        }

        private IPAddress? GetClientIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress;
        }
    }
}
