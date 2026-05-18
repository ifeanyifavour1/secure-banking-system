using BankingApi.Data;
using BankingApi.DTOs;
using Microsoft.AspNetCore.Mvc;

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

            // Next: hash password, create customer user, save, and write audit log.
            await Task.CompletedTask;

            return Ok(new { message = "Registration endpoint is ready for implementation." });
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
