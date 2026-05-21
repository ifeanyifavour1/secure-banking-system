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


using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using BankingApi.Auth;
using BankingApi.Data;
using BankingApi.Middleware;
using BankingApi.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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

var connectionString = ResolveDatabaseConnectionString(builder.Configuration);
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database is not configured. Copy project/.env.example to project/.env and set "
        + "ConnectionStrings__neondb from your Neon dashboard (Connection details → .NET).");
}

if (!TryGetConnectionHost(connectionString, out var dbHost))
{
    throw new InvalidOperationException(
        "ConnectionStrings__neondb must use Neon's .NET connection string (Host=ep-....neon.tech;Database=...;Username=...;Password=...). "
        + "If you only have a postgresql:// URL, set DATABASE_URL instead, or paste the .NET string from Neon → Connection details → .NET.");
}

var envFilePath = FindEnvFilePath();
if (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"[BankingApi] .env: {(envFilePath ?? "(not found — copy project/.env.example to project/.env)")}");
    Console.WriteLine($"[BankingApi] Database host: {dbHost}");
}

await ValidateDatabaseHostAsync(dbHost);

builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection("Admin"));
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<TransactionService>();

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

var corsOrigins = builder.Configuration["Cors:AllowedOrigins"]
    ?? builder.Configuration["Frontend:Url"]
    ?? "http://localhost:5000";
var allowedOrigins = corsOrigins
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
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

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAuditLogging();

app.MapGet("/health/db", async (BankingDbContext db) =>
{
    try
    {
        await db.Database.OpenConnectionAsync();
        await db.Database.CloseConnectionAsync();

        return Results.Ok(new { status = "ok", database = "connected", host = dbHost });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database connection failed.",
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
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

static string? ResolveDatabaseConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("neondb");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        return NormalizeDatabaseConnectionString(connectionString);
    }

    var databaseUrl = configuration["DATABASE_URL"];
    if (!string.IsNullOrWhiteSpace(databaseUrl)
        && !databaseUrl.Contains("xxxxx", StringComparison.OrdinalIgnoreCase))
    {
        return NormalizeDatabaseConnectionString(databaseUrl);
    }

    return null;
}

static string? NormalizeDatabaseConnectionString(string value)
{
    var trimmed = value.Trim();
    if (trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
        || trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    {
        return ConvertDatabaseUrlToNpgsql(trimmed);
    }

    return trimmed;
}

static string? ConvertDatabaseUrlToNpgsql(string databaseUrl)
{
    if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri)
        || uri.Scheme is not ("postgresql" or "postgres"))
    {
        return null;
    }

    var host = uri.Host;
    var port = uri.IsDefaultPort ? 5432 : uri.Port;
    var database = uri.AbsolutePath.TrimStart('/');
    var userInfo = uri.UserInfo.Split(':', 2);
    var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

    return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require";
}

static bool TryGetConnectionHost(string connectionString, out string host)
{
    host = string.Empty;

    foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && (parts[0].Equals("Host", StringComparison.OrdinalIgnoreCase)
                || parts[0].Equals("Server", StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(parts[1]))
        {
            host = parts[1];
            return true;
        }
    }

    return false;
}

static void LoadEnvFile()
{
    var envPath = FindEnvFilePath();
    if (envPath is null)
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

        // Always apply database settings from project/.env (avoids stale machine-wide vars).
        var forceFromFile = key.Equals("ConnectionStrings__neondb", StringComparison.OrdinalIgnoreCase)
            || key.Equals("DATABASE_URL", StringComparison.OrdinalIgnoreCase);

        if (forceFromFile || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

static async Task ValidateDatabaseHostAsync(string host)
{
    try
    {
        var addresses = await Dns.GetHostAddressesAsync(host);
        if (addresses.Length == 0)
        {
            throw new InvalidOperationException($"Database host '{host}' did not resolve to any IP address.");
        }
    }
    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
    {
        throw new InvalidOperationException(
            $"Cannot resolve database host '{host}'. "
            + "Check internet/VPN/DNS, update ConnectionStrings__neondb in project/.env from Neon, then restart the API.",
            ex);
    }
}

static string? FindEnvFilePath()
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var directories = new List<string>
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    };

    foreach (var startDir in directories)
    {
        var current = startDir;

        for (var depth = 0; depth < 8 && !string.IsNullOrEmpty(current); depth++)
        {
            var candidate = Path.Combine(current, ".env");
            if (seen.Add(candidate) && File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            current = parent?.FullName ?? string.Empty;
        }
    }

    return null;
}

