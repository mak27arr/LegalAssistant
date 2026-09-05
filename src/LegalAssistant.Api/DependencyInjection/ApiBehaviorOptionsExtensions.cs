using LegalAssistant.Api.Errors;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.DependencyInjection;

public static class ApiBehaviorOptionsExtensions
{
    public static void ConfigureValidationProblemDetails(this ApiBehaviorOptions options)
    {
        options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(ApiProblemDetailsFactory.CreateValidationProblemDetails(context))
            {
                ContentTypes = { "application/problem+json" }
            };
    }
}
