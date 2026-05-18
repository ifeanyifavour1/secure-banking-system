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

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            // Next: find user, verify password, apply lockout/MFA, then issue tokens.
            await Task.CompletedTask;

            return Ok(new { message = "Login endpoint is ready for implementation." });
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
