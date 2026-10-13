using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Infrastructure.Documents;

public sealed class DocumentUrlValidator : IDocumentUrlValidator
{
    private readonly DocumentFetchOptions _options;

    public DocumentUrlValidator(IOptions<DocumentFetchOptions> options)
    {
        _options = options.Value;
    }

    public async Task ValidateAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableUrlValidation)
            return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Document URL must be an absolute URI.", nameof(url));

        if (!string.IsNullOrWhiteSpace(uri.Scheme) && _options.AllowedSchemes.Length > 0 &&
            !_options.AllowedSchemes.Any(s => string.Equals(s, uri.Scheme, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Document URL scheme '{uri.Scheme}' is not allowed.", nameof(url));
        }

        if (_options.AllowedHosts.Length > 0 &&
            !_options.AllowedHosts.Any(host => string.Equals(host, uri.Host, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Document URL host '{uri.Host}' is not allowed.", nameof(url));
        }

        if (_options.BlockPrivateNetworkAddresses && await IsPrivateAddressAsync(uri, cancellationToken))
            throw new ArgumentException("Document URL resolves to a private or loopback address.", nameof(url));
    }

    private static async Task<bool> IsPrivateAddressAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(uri.Host, out var parsed))
            return IsPrivate(parsed);

        var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        return addresses.Any(IsPrivate);
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] is 10
                || (bytes[0] is 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] is 192 && bytes[1] is 168)
                || (bytes[0] is 169 && bytes[1] is 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return (bytes[0] & 0xFE) == 0xFC
                || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
                || address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.Equals(IPAddress.IPv6Loopback);
        }

        return false;
    }
}
