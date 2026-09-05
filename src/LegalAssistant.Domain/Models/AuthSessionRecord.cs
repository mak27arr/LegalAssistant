namespace LegalAssistant.Domain.Models;

public class AuthSessionRecord
{
    public required string Id { get; set; }
    public Guid UserId { get; set; }
    public required byte[] Ticket { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastRenewedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public User? User { get; set; }
}
