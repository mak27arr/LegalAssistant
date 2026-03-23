namespace LegalAssistant.Application.Chunks.Models;

public sealed record CharRange(int Start, int EndExclusive)
{
    public int Length => EndExclusive - Start;
}
