namespace LegalAssistant.Application.Chunking.Models;

public sealed record ChunkingRunDescriptor(string StrategyId, string StrategyName, string StrategyVersion, string ParamsJson);
