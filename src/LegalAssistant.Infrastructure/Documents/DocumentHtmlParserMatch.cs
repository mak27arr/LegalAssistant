using System;

namespace LegalAssistant.Infrastructure.Documents;

public sealed record DocumentHtmlParserMatch(
    bool IsMatch,
    int Specificity,
    int UrlMatchScore,
    int Priority,
    int Confidence,
    string? Reason = null)
{
    public static DocumentHtmlParserMatch NoMatch(string? reason = null) =>
        new(false, 0, 0, 0, 0, reason);

    public static DocumentHtmlParserMatch Match(
        int specificity,
        int urlMatchScore,
        int priority,
        int confidence,
        string? reason = null)
    {
        if (specificity < 0) throw new ArgumentOutOfRangeException(nameof(specificity));
        if (urlMatchScore < 0) throw new ArgumentOutOfRangeException(nameof(urlMatchScore));
        if (confidence < 0) throw new ArgumentOutOfRangeException(nameof(confidence));

        return new DocumentHtmlParserMatch(true, specificity, urlMatchScore, priority, confidence, reason);
    }
}
