namespace LegalAssistant.Domain.Models;

public class DataProtectionKeyRecord
{
    public int Id { get; set; }
    public required string FriendlyName { get; set; }
    public required string Xml { get; set; }
}
