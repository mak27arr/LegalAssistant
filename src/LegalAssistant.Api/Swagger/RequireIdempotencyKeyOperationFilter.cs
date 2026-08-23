using System.Reflection;
using LegalAssistant.Api.Filters;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LegalAssistant.Api.Swagger;

public sealed class RequireIdempotencyKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresIdempotencyKey =
            context.MethodInfo.GetCustomAttribute<RequireIdempotencyKeyAttribute>(inherit: true) != null ||
            context.MethodInfo.DeclaringType?.GetCustomAttribute<RequireIdempotencyKeyAttribute>(inherit: true) != null;

        if (!requiresIdempotencyKey)
            return;

        operation.Parameters ??= [];

        if (operation.Parameters.Any(p => string.Equals(p.Name, RequireIdempotencyKeyAttribute.HeaderName, StringComparison.OrdinalIgnoreCase)))
            return;

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = RequireIdempotencyKeyAttribute.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Unique key used to deduplicate repeated ask submissions.",
            Schema = new OpenApiSchema
            {
                Type = "string"
            }
        });
    }
}
