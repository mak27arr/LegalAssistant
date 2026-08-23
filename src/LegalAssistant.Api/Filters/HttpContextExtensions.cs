namespace LegalAssistant.Api.Filters;

public static class HttpContextExtensions
{
    public static string GetRequiredIdempotencyKey(this HttpContext context)
        => context.Items.TryGetValue(RequireIdempotencyKeyAttribute.ItemKey, out var value) && value is string key && !string.IsNullOrWhiteSpace(key)
            ? key
            : throw new InvalidOperationException("Idempotency key was not populated by the request filter.");
}
