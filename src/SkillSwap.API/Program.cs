using SkillSwap.API.Extensions;
using SkillSwap.API.Hubs;
using SkillSwap.API.Middleware;
using SkillSwap.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Services ────────────────────────────────────────────────────────────────

builder.Services.AddControllers();

// Infrastructure (EF Core, Identity, JWT service, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

// Authentication & Authorization (JWT Bearer)
builder.Services.AddSkillSwapAuthentication(builder.Configuration);

// Swagger / OpenAPI with JWT support
builder.Services.AddSkillSwapSwagger();

// SignalR
builder.Services.AddSignalR();

// Health checks
builder.Services.AddHealthChecks();

// CORS - configure allowed origins before production deployment
builder.Services.AddCors(options =>
{
    options.AddPolicy("SkillSwapPolicy", policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// ── Middleware Pipeline ──────────────────────────────────────────────────────

// Global exception handling - must be first
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSkillSwapSwagger();
}

app.UseHttpsRedirection();
app.UseCors("SkillSwapPolicy");
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────────────────────

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHealthChecks("/health");

app.Run();

// Expose for test integration
public partial class Program { }
