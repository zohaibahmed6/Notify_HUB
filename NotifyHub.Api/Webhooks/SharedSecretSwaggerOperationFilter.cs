using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NotifyHub.Api.Webhooks;

/// Makes [SharedSecret]-protected endpoints (WebhooksController, MockGatewayController's
/// gateway-receipt callback) testable from Swagger UI — without this, "Try it out" has no
/// field for the required X-Webhook-Secret header and every call 401s regardless of body.
public class SharedSecretSwaggerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!context.ApiDescription.CustomAttributes().OfType<SharedSecretAttribute>().Any())
            return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Webhook-Secret",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Must match the server's Webhooks:SharedSecret config value.",
            Schema = new OpenApiSchema { Type = "string" },
        });
    }
}
