using Microsoft.EntityFrameworkCore;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing required configuration: ConnectionStrings:Default");

builder.Services.AddDbContext<NotifyHubDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35))));

builder.Services.AddHostedService<PlaceholderHeartbeatWorker>();

var host = builder.Build();
host.Run();
