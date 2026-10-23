using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShopFinder.Api;
using ShopFinder.Api.Data;
using ShopFinder.Api.Endpoints;
using ShopFinder.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Refuse to run in production with the sample signing key.
if (!builder.Environment.IsDevelopment() && (config["Jwt:Key"] ?? "").StartsWith("CHANGE-ME"))
    throw new InvalidOperationException(
        "Set a real Jwt:Key (32+ random characters) via appsettings.Production.json or the Jwt__Key environment variable.");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=shopfinder.db"));

builder.Services.AddSingleton<Clock>();
builder.Services.AddSingleton<TokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep our "shopId" claim name as-is
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TokenService.SigningKey(config),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// Only needed when the React dev server (Vite, port 5173) calls the API directly.
var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// Basic abuse protection for login/sign-up and "report closed".
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("reports", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Create the database on first run and add demo shops.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (config.GetValue("App:SeedDemoData", true))
        DbSeeder.Seed(db, config, scope.ServiceProvider.GetRequiredService<Clock>());
}

// Turn ApiError into a JSON { error } response with the right status code.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (ApiError e) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = e.Status;
        await ctx.Response.WriteAsJsonAsync(new { error = e.Message });
    }
    catch (BadHttpRequestException e) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new { error = "Invalid request: " + e.Message });
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();          // serves the built React app from wwwroot
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapPublicEndpoints();
app.MapOwnerEndpoints();

// Unknown /api routes -> 404 JSON; everything else -> React app (client-side routing).
app.MapFallback("/api/{**rest}", () => Results.NotFound(new { error = "Not found." }));
app.MapFallbackToFile("index.html");

app.Run();
