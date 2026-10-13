using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalAssistant.Infrastructure.Db.Migrations;

[DbContext(typeof(LegalAssistantDbContext))]
[Migration("20260906200000_AddOutboxNotifications")]
partial class AddOutboxNotifications
{
}
