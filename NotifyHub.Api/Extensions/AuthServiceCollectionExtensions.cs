using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NotifyHub.Api.Auth;

namespace NotifyHub.Api.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddNotifyHubJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration["Jwt:Secret"]))
            throw new InvalidOperationException("Missing required configuration: Jwt:Secret");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Bound lazily via IOptions<JwtOptions>, resolved at first use (same timing/snapshot
        // JwtTokenService reads) rather than eagerly at registration time — keeps the signing
        // key used to issue tokens and the key used to validate them derived from the exact
        // same configuration snapshot even if configuration sources are still being layered in
        // (e.g. test hosts appending overrides after this method runs).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // SignalR's browser client can't set an Authorization header on the
                // WebSocket handshake, so the JWT is passed as an "access_token" query
                // param instead (standard SignalR pattern, §6a) — only honored for the
                // hub path, so this doesn't weaken auth on the regular API routes.
                bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        // Every endpoint requires authorization by default; [AllowAnonymous] opts individual
        // actions (login, refresh) out. Safer default posture than opting in per-controller.
        services.AddAuthorization();
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add(new AuthorizeFilter());
        });

        return services;
    }
}
