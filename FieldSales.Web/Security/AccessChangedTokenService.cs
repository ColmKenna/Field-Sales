using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace FieldSales.Web.Security;

public sealed record AccessChangedState(string Subject, string AreaKey, bool WasWrite, DateTimeOffset ExpiresUtc);

public sealed class AccessChangedTokenService(IDataProtectionProvider dataProtection, TimeProvider clock)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("FieldSales.Web.AccessChanged.v1");

    public string Create(string subject, string areaKey, bool wasWrite) =>
        _protector.Protect(JsonSerializer.Serialize(new AccessChangedState(
            subject, areaKey, wasWrite, clock.GetUtcNow().AddMinutes(5))));

    public AccessChangedState? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            AccessChangedState? state = JsonSerializer.Deserialize<AccessChangedState>(_protector.Unprotect(token));
            return state is not null && state.ExpiresUtc > clock.GetUtcNow() ? state : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }
}
