using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LegalAssistant.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireIdempotencyKeyAttribute : ActionFilterAttribute
{
    public const string HeaderName = "Idempotency-Key";
    public const string ItemKey = "IdempotencyKey";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var raw = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            context.Result = new BadRequestObjectResult($"{HeaderName} header is required.");
            return;
        }

        context.HttpContext.Items[ItemKey] = raw.Trim();
    }
}
