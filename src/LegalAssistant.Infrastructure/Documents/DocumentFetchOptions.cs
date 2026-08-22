namespace LegalAssistant.Infrastructure.Documents;

public sealed class DocumentFetchOptions
{
    public bool EnableUrlValidation { get; set; }

    public bool BlockPrivateNetworkAddresses { get; set; }

    public string[] AllowedSchemes { get; set; } = [];

    public string[] AllowedHosts { get; set; } = [];

    public int? RequestTimeoutSeconds { get; set; }

    public long MaxResponseBytes { get; set; }
}
