using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotifyHub.Api.Extensions;
using NotifyHub.Api.Gateway;
using NotifyHub.Api.Inbox;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste the access token (without the \"Bearer \" prefix).",
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing required configuration: ConnectionStrings:Default");

builder.Services.AddDbContext<NotifyHubDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddScoped<IDbSeedStep, UserSeedStep>();
builder.Services.AddScoped<IDbSeedStep, SecondStaffSeedStep>();
builder.Services.AddScoped<IDbSeedStep, PatientAppointmentSeedStep>();
builder.Services.AddScoped<IDbSeedStep, TemplateSeedStep>();
builder.Services.AddScoped<IDbSeedStep, DemoOutboundMessageSeedStep>();
// FR-010: default 50,000 in production; test factories override Seed:PerformanceMessageCount
// to a small number so booting the Api pipeline in every integration test doesn't also seed
// 50k rows each time (see CustomWebApplicationFactory/MySqlWebApplicationFactory).
builder.Services.AddScoped<IDbSeedStep>(sp =>
    new PerformanceSeedStep(builder.Configuration.GetValue("Seed:PerformanceMessageCount", 50_000)));
builder.Services.AddScoped<DbSeedRunner>();

builder.Services.AddNotifyHubJwtAuth(builder.Configuration);
builder.Services.AddSignalR();

builder.Services.Configure<MockGatewayOptions>(builder.Configuration.GetSection(MockGatewayOptions.SectionName));
builder.Services.AddHttpClient("self", (services, client) =>
{
    var opts = services.GetRequiredService<IOptions<MockGatewayOptions>>().Value;
    client.BaseAddress = new Uri(opts.CallbackBaseUrl);
    client.DefaultRequestHeaders.Add("X-Webhook-Secret", builder.Configuration["Webhooks:SharedSecret"]);
});

// Comma-separated so the same dev deployment can serve both a developer's own browser
// (http://localhost:5173) and the Playwright e2e suite, which loads the SPA from the
// docker-compose service hostname (http://web:5173) — CORS is origin-based, so both
// need to be explicitly allowed, not just whichever one a human happens to use.
var webOrigins = (builder.Configuration["Cors:WebOrigin"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebApp", policy =>
    {
        if (webOrigins.Length > 0)
            policy.WithOrigins(webOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

// EF Core migrations apply automatically on startup (§11), followed by one-time,
// idempotent seed steps. This runs before Kestrel starts listening.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

    // The InMemory provider (used by integration tests) doesn't support relational
    // migrations; EnsureCreated is the test-only equivalent. Production always uses
    // the MySQL relational provider and takes the Migrate path.
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    var seedRunner = scope.ServiceProvider.GetRequiredService<DbSeedRunner>();
    await seedRunner.RunAsync(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseCors("WebApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<InboxHub>("/hubs/inbox");

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
