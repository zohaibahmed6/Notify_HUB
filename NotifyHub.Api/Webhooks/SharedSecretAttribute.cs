using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NotifyHub.Api.Webhooks;

/// Protects service-to-service endpoints that can't carry a JWT (external webhook
/// callers, and the Worker/mock-gateway calling back into Api) with a shared-secret
/// header instead. Pair with [AllowAnonymous] to opt the action out of the default
/// JWT requirement (§10: "Webhook endpoints require shared-secret header").
public class SharedSecretAttribute : Attribute, IAsyncActionFilter
{
    private const string HeaderName = "X-Webhook-Secret";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = configuration["Webhooks:SharedSecret"];
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        var expectedBytes = Encoding.UTF8.GetBytes(expected ?? string.Empty);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        if (string.IsNullOrEmpty(expected) ||
            expectedBytes.Length != providedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
