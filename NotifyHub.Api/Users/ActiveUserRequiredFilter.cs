using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Users;

/// §7: Inactive/OnLeave users get read-only access until reactivated. Registered globally
/// (`MvcOptions.Filters.Add`, same mechanism as the default `AuthorizeFilter`) rather than
/// as an opt-in attribute like `SharedSecretAttribute` — every current and future
/// controller is covered automatically, nothing can forget to apply it.
///
/// Checks the caller's live `Status` from the DB on every mutating request instead of
/// trusting the JWT's claims — the access token is valid for up to 30 minutes
/// (`Jwt:AccessTokenMinutes`), so a claims-based check would let a just-deactivated user
/// keep mutating for up to half an hour. A single indexed PK lookup per mutating request is
/// not a meaningful perf cost at this scale.
///
/// Safe HTTP methods and any `[AllowAnonymous]` action (login/refresh/logout, webhooks,
/// mock-gateway) are skipped entirely — a deactivated/on-leave user must still be able to
/// log in and out (the requirement is "read-only access," not "locked out").
public class ActiveUserRequiredFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (SafeMethods.Contains(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next();
            return;
        }

        var userIdClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !long.TryParse(userIdClaim, out var userId))
        {
            await next();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<NotifyHubDbContext>();
        var status = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => (UserStatus?)u.Status)
            .SingleOrDefaultAsync();

        if (status is not null && status != UserStatus.Active)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Title = "Your account is not active. Contact an administrator.",
                Status = StatusCodes.Status403Forbidden,
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}
