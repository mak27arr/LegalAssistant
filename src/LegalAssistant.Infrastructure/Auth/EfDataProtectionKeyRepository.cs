using System.Xml.Linq;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAssistant.Infrastructure.Auth;

public sealed class EfDataProtectionKeyRepository : IXmlRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfDataProtectionKeyRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();

        return db.DataProtectionKeys
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => XElement.Parse(x.Xml))
            .ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();

        var record = db.DataProtectionKeys.SingleOrDefault(x => x.FriendlyName == friendlyName);
        if (record == null)
        {
            db.DataProtectionKeys.Add(new DataProtectionKeyRecord
            {
                FriendlyName = friendlyName,
                Xml = element.ToString(SaveOptions.DisableFormatting)
            });
        }
        else
        {
            record.Xml = element.ToString(SaveOptions.DisableFormatting);
        }

        db.SaveChanges();
    }
}
