namespace LegalAssistant.Application.Ask.Models;

public sealed record AskQuery(string Question, int TopK = 5);
