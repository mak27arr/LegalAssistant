using System.Text.RegularExpressions;
using LegalAssistant.Domain.Documents;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class RegexHtmlToTextConverter : IHtmlToTextConverter
{
    public string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        html = Regex.Replace(html, "<(script|style)[^>]*?>.*?</\\1>", string.Empty,
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        html = Regex.Replace(html, "<.*?>", string.Empty);

        html = System.Net.WebUtility.HtmlDecode(html);

        return Regex.Replace(html, "\\s+", " ").Trim();
    }
}
