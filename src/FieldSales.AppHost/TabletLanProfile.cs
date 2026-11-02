using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace FieldSales.AppHost;

/// <summary>Opt-in development endpoints shared by the tablet browser, API and identity server.</summary>
internal sealed record TabletLanProfile(string Host, string CertificatePath)
{
    public string IdentityUrl => $"https://{Host}:7201";
    public string ApiUrl => $"https://{Host}:7204";

    public static TabletLanProfile? Read(IConfiguration configuration, bool development)
    {
        if (!configuration.GetValue<bool>("TabletLan:Enabled")) return null;
        if (!development) throw new InvalidOperationException("Tablet LAN access is only available in Development.");
        string? host = configuration["TabletLan:Host"];
        if (!IPAddress.TryParse(host, out IPAddress? address) || address.AddressFamily != AddressFamily.InterNetwork
            || IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.Broadcast)
            || address.GetAddressBytes()[0] >= 224)
            throw new InvalidOperationException("TabletLan:Host must be this Mac's LAN IPv4 address, without a scheme, port or path.");
        string? certificate = configuration["TabletLan:CertificatePath"];
        if (string.IsNullOrWhiteSpace(certificate) || !Path.IsPathFullyQualified(certificate) || !File.Exists(certificate))
            throw new InvalidOperationException("TabletLan:CertificatePath must name an existing absolute HTTPS certificate path.");
        if (string.IsNullOrWhiteSpace(configuration["Parameters:tablet-lan-certificate-password"]))
            throw new InvalidOperationException("Set the user secret Parameters:tablet-lan-certificate-password before enabling Tablet LAN access.");
        return new(address.ToString(), certificate);
    }
}
