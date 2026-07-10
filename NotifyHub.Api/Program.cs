using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Extensions;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing required configuration: ConnectionStrings:Default");

builder.Services.AddDbContext<NotifyHubDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35))));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddScoped<IDbSeedStep, UserSeedStep>();
builder.Services.AddScoped<DbSeedRunner>();

builder.Services.AddNotifyHubJwtAuth(builder.Configuration);

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
    await db.Database.MigrateAsync();

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
