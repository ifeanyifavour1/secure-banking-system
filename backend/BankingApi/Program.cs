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


using BankingApi.Data;
using Microsoft.EntityFrameworkCore;

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("neondb");

builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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

