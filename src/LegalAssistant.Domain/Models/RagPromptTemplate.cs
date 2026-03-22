using System;

namespace LegalAssistant.Domain.Models;

public sealed class RagPromptTemplate
{
    public Guid Id { get; set; }
    public string SystemHeader { get; set; } = string.Empty;
    public string InstructionsFooter { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
