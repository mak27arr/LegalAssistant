namespace LegalAssistant.Api.Dtos.Ask;

public sealed record AskRequest(string Question, int? TopK);

public sealed record AskChunkDto(Guid ChunkId, Guid DocumentId, int ChunkIndex, string Text, string? SourceUrl, double Score);

public sealed record AskResponse(string Question, int TopK, string Answer, IReadOnlyList<AskChunkDto> Chunks, string Prompt);

public sealed record AskPromptResponse(string Question, int TopK, string Prompt, IReadOnlyList<AskChunkDto> Chunks);
