using Microsoft.EntityFrameworkCore;
using NotifyHub.Infrastructure.Escalation;
using NotifyHub.Infrastructure.Messaging;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing required configuration: ConnectionStrings:Default");

builder.Services.AddDbContext<NotifyHubDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35))));

builder.Services.AddHttpClient("gateway", (services, client) =>
{
    var baseUrl = services.GetRequiredService<IConfiguration>()["MockGateway:ApiBaseUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("X-Webhook-Secret", builder.Configuration["Webhooks:SharedSecret"]);
});

builder.Services.AddScoped(sp => new MessageDispatcher(
    sp.GetRequiredService<NotifyHubDbContext>(),
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("gateway"),
    sp.GetRequiredService<ILogger<MessageDispatcher>>()));

builder.Services.AddHostedService<DispatcherWorker>();

builder.Services.AddScoped<EscalationJob>();
builder.Services.AddHostedService<EscalationWorker>();

var host = builder.Build();
host.Run();
