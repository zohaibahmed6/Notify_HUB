using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotifyHub.Api.Extensions;
using NotifyHub.Api.Gateway;
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
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35))));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddScoped<IDbSeedStep, UserSeedStep>();
builder.Services.AddScoped<IDbSeedStep, PatientAppointmentSeedStep>();
builder.Services.AddScoped<IDbSeedStep, TemplateSeedStep>();
builder.Services.AddScoped<IDbSeedStep, DemoOutboundMessageSeedStep>();
builder.Services.AddScoped<DbSeedRunner>();

builder.Services.AddNotifyHubJwtAuth(builder.Configuration);

builder.Services.Configure<MockGatewayOptions>(builder.Configuration.GetSection(MockGatewayOptions.SectionName));
builder.Services.AddHttpClient("self", (services, client) =>
{
    var opts = services.GetRequiredService<IOptions<MockGatewayOptions>>().Value;
    client.BaseAddress = new Uri(opts.CallbackBaseUrl);
    client.DefaultRequestHeaders.Add("X-Webhook-Secret", builder.Configuration["Webhooks:SharedSecret"]);
});

var webOrigin = builder.Configuration["Cors:WebOrigin"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebApp", policy =>
    {
        if (!string.IsNullOrWhiteSpace(webOrigin))
            policy.WithOrigins(webOrigin).AllowAnyHeader().AllowAnyMethod();
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

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
