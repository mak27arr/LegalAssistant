using System;

namespace LegalAssistant.Application.Chunking.Models;

public sealed record ChunkingRunContext(Guid DocumentId, string? SourceUrl);
