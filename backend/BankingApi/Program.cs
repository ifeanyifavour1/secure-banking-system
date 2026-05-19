// Entry point for the C# .NET Core Web API
//
// What to implement here:
// - Configure JWT Bearer authentication (short-lived access tokens)
// - Define RBAC authorization policies: AdminOnly, ManagerOrAbove, TellerOrAbove
// - Enable CORS for the Flask frontend origin
// - Enforce HTTPS redirection and HSTS headers
// - Register EF Core DbContext with Npgsql (PostgreSQL on Neon)
// - Register services: TransactionService, JwtTokenService
// - Add Swagger for API documentation in development


using System.Security.Authentication;
using System.Text;
using BankingApi.Auth;
using BankingApi.Data;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ConfigureHttpsDefaults(listenOptions =>
    {
        // Production: TLS 1.3 only. Development: also allow TLS 1.2 for local dev certs.
        listenOptions.SslProtocols = builder.Environment.IsDevelopment()
            ? SslProtocols.Tls12 | SslProtocols.Tls13
            : SslProtocols.Tls13;
    });
});

var httpsPort = builder.Configuration.GetValue<int?>("Https:Port")
    ?? (builder.Environment.IsDevelopment() ? 7285 : 443);

builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = httpsPort;
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("neondb");

builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<JwtTokenService>();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are not configured.");

if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
{
    throw new InvalidOperationException("JWT secret is not configured.");
}

var signingKeyBytes = TryGetSigningKeyBytes(jwtSettings.Secret);
var signingKey = new SymmetricSecurityKey(signingKeyBytes);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("ManagerOrAbove", policy => policy.RequireRole("admin", "manager"));
    options.AddPolicy("TellerOrAbove", policy => policy.RequireRole("admin", "manager", "teller"));
});

var frontendUrl = builder.Configuration["Frontend:Url"] ?? "http://localhost:5000";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(frontendUrl)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 20;
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/db", async (BankingDbContext db) =>
{
    try
    {
        await db.Database.OpenConnectionAsync();
        await db.Database.CloseConnectionAsync();

        return Results.Ok(new { status = "ok", database = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database connection failed.",
            detail: ex.Message);
    }
});

app.MapGet("/health/db/tables", async (BankingDbContext db) =>
{
    var states = await db.TransactionStates
        .Select(s => s.StateName)
        .ToListAsync();

    return Results.Ok(new
    {
        status = "ok",
        transactionStates = states
    });
});

app.MapControllers();

app.Run();

static byte[] TryGetSigningKeyBytes(string secret)
{
    try
    {
        return Convert.FromBase64String(secret);
    }
    catch (FormatException)
    {
        return Encoding.UTF8.GetBytes(secret);
    }
}

static void LoadEnvFile()
{
    var envPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env"));

    if (!File.Exists(envPath))
    {
        return;
    }

    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmedLine = line.Trim();

        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = trimmedLine.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = trimmedLine[..separatorIndex].Trim();
        var value = trimmedLine[(separatorIndex + 1)..].Trim().Trim('"');

        Environment.SetEnvironmentVariable(key, value);
    }
}

