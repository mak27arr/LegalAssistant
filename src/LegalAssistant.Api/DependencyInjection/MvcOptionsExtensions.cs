using LegalAssistant.Api.Filters;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.DependencyInjection;

public static class MvcOptionsExtensions
{
    public static void AddGlobalFilters(this MvcOptions options)
    {
        options.Filters.Add<ValidateAntiforgeryTokenFilter>();
    }
}
