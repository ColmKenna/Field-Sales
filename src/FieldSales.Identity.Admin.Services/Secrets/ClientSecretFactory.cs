using Duende.IdentityServer.Models;
using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.Entities;

namespace FieldSales.Identity.Services.Secrets;

public sealed record GeneratedClientSecret(string Plaintext, ClientSecret Secret);

public static class ClientSecretFactory
{
    public static GeneratedClientSecret Create(TimeProvider clock, string? description, DateTime? expiration = null)
    {
        string plaintext = CryptoRandom.CreateUniqueId();
        return new(plaintext, new ClientSecret
        {
            Description = description,
            Value = plaintext.Sha256(),
            Type = SecretType.SharedSecret.ToSecretTypeValue(),
            Created = clock.GetUtcNow().UtcDateTime,
            Expiration = expiration?.ToUniversalTime()
        });
    }
}
