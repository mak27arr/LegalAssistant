using System;
using System.Collections.Generic;

namespace LegalAssistant.Domain.Models;

public sealed class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required string GoogleSubjectId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
