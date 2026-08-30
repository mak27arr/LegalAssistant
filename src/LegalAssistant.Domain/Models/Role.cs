using System;
using System.Collections.Generic;

namespace LegalAssistant.Domain.Models;

public sealed class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
