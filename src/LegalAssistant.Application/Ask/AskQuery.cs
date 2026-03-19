namespace LegalAssistant.Application.Ask;

public sealed record AskQuery(string Question, int TopK = 5);
