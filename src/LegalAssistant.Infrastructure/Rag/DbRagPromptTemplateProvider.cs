using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Rag;

public sealed class DbRagPromptTemplateProvider : IRagPromptTemplateProvider
{
    private readonly LegalAssistantDbContext _db;

    public DbRagPromptTemplateProvider(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<RagPromptTemplateDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.RagPromptTemplates
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
             .FirstOrDefaultAsync(cancellationToken);

        if (row != null)
            return new RagPromptTemplateDto(row.SystemHeader, row.InstructionsFooter);

        return new RagPromptTemplateDto(
            "Ти юридичний асистент. Відповідай українською. Якщо інформації в джерелах недостатньо — скажи про це.",
            "- Дай коротку відповідь + деталізацію пунктами.\n- Додай посилання на джерела у вигляді [1], [2] де доречно.");
    }
}
