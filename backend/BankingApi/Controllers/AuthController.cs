using BankingApi.Data;
using BankingApi.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BankingApi.Models;

namespace BankingApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private const int MaxFailedLoginAttempts = 5;

        private readonly BankingDbContext _db;

        public AuthController(BankingDbContext db)
        {
            _db = db;
        }

[HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            // Check if unique database fields already exist
            var emailExists = await _db.Users.AnyAsync(u => u.Email == request.Email);
            if (emailExists)
            {
                return BadRequest(new { message = "Email is already registered." });
            }

            var nationalIdExists = await _db.Users.AnyAsync(u => u.NationalId == request.NationalId);
            if (nationalIdExists)
            {
                return BadRequest(new { message = "National ID is already registered." });
            }

            // Hash password to match the team's BCrypt byte[] implementation
            string salt = BCrypt.Net.BCrypt.GenerateSalt(12);
            string hashedStr = BCrypt.Net.BCrypt.HashPassword(request.Password, salt);
            byte[] passwordHashBytes = System.Text.Encoding.UTF8.GetBytes(hashedStr);

            // Create customer user matching User.cs properties
            var newUser = new User
            {
                UserId = Guid.NewGuid(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = passwordHashBytes,
                PasswordSalt = Array.Empty<byte>(), 
                DateOfBirth = request.DateOfBirth,
                NationalId = request.NationalId,
                AddressLine1 = request.AddressLine1,
                AddressLine2 = request.AddressLine2,
                City = request.City,
                Country = request.Country,
                PostalCode = request.PostalCode,
                Role = "customer", 
                MfaEnabled = false,
                MfaSecret = null,
                IsActive = false,
                IsLocked = false,
                FailedLoginCount = 0,
                LastLoginAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Users.Add(newUser);

            // Write audit log entry matching AuditLogEntry.cs properties
            var auditEntry = new AuditLogEntry
            {
                EventType = "SECURITY",
                EntityType = "User",
                EntityId = newUser.UserId.ToString(), 
                Action = "USER_REGISTER",
                PerformedBy = newUser.UserId, 
                OldValues = null,
                NewValues = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { 
                    email = newUser.Email, 
                    role = newUser.Role, 
                    ip = HttpContext.Connection.RemoteIpAddress?.ToString() 
                })), 
                IpAddress = HttpContext.Connection.RemoteIpAddress, 
                UserAgent = Request.Headers["User-Agent"].ToString(),
                AdditionalInfo = System.Text.Json.JsonDocument.Parse("{}"), 
                CreatedAt = DateTime.UtcNow
            };

            _db.AuditLogEntries.Add(auditEntry);

            // Save changes to database
            await _db.SaveChangesAsync();

            return Ok(new { message = "Registration successful!" });
        }

//password     
[HttpPost("login")]
public async Task<IActionResult> Login(LoginRequest request)
{
    if (!ModelState.IsValid)
    {
        return ValidationProblem(ModelState);
    }

    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

    if (user == null)
    {
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

    bool passwordValid = VerifyPassword(request.Password, user.PasswordHash);

    if (!passwordValid)
    {
        user.FailedLoginCount += 1;

        if (user.FailedLoginCount >= MaxFailedLoginAttempts)
        {
            user.IsLocked = true;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Unauthorized(new { message = "Invalid email or password." });
    }

    user.FailedLoginCount = 0;
    user.LastLoginAt = DateTime.UtcNow;
    user.UpdatedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();

    return Ok(new { message = "Login successful.", userId = user.UserId, role = user.Role });
}

private bool VerifyPassword(string password, byte[] passwordHash)
{
    string hash = System.Text.Encoding.UTF8.GetString(passwordHash);
    return BCrypt.Net.BCrypt.Verify(password, hash);
}


        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            // Next: validate refresh token and rotate access/refresh token pair.
            await Task.CompletedTask;

            return Ok(new { message = "Refresh endpoint is ready for implementation." });
        }
    }
}
