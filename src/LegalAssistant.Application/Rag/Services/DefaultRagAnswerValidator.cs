using System.Text.RegularExpressions;
using LegalAssistant.Application.Rag.Models;

namespace LegalAssistant.Application.Rag.Services;

public sealed class DefaultRagAnswerValidator : IRagAnswerValidator
{
    private static readonly Regex CitationRegex = new(@"\[(\d+)\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public RagAnswerValidationResult Validate(string answer, IReadOnlyList<RagAnswerSource> sources)
    {
        var issues = new List<string>();
        var citations = new HashSet<int>();

        if (string.IsNullOrWhiteSpace(answer))
        {
            issues.Add("Answer is empty.");
            return new RagAnswerValidationResult(false, Array.Empty<int>(), issues);
        }

        foreach (Match match in CitationRegex.Matches(answer))
        {
            if (!int.TryParse(match.Groups[1].Value, out var citationId))
                continue;

            citations.Add(citationId);

            if (citationId < 1 || citationId > sources.Count)
            {
                issues.Add($"Citation [{citationId}] does not match any retrieved chunk.");
            }
        }

        if (citations.Count == 0)
            issues.Add("Answer does not contain any citations.");

        return new RagAnswerValidationResult(issues.Count == 0, citations.OrderBy(x => x).ToArray(), issues);
    }
}
