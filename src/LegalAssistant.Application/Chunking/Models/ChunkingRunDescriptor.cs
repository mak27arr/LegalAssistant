namespace LegalAssistant.Application.Chunking.Models;

public sealed record ChunkingRunDescriptor(string StrategyName, string StrategyVersion, string ParamsJson);
