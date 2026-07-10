using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.Tokens;
using NotifyHub.Api.Auth;

namespace NotifyHub.Api.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddNotifyHubJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing required configuration section: Jwt");

        if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
            throw new InvalidOperationException("Missing required configuration: Jwt:Secret");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
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
