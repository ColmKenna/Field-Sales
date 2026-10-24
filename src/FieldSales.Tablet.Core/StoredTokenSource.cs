using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>Returns the rep's access token, refreshing it with the one-time refresh token when it has expired.</summary>
/// <remarks>
/// A refused refresh (expired after 24 hours, or revoked) clears the tokens, so Sync asks for a connected sign-in.
/// A network failure is left to Sync, which reports no connection. Refreshes for one rep never overlap, because
/// each refresh token can be used only once.
/// </remarks>
internal sealed class StoredTokenSource(ISecureStore secure, string subject, HttpClient identity, TimeProvider clock) : IAccessTokenSource
{
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        SemaphoreSlim gate = Gates.GetOrAdd(subject, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (await TokenStore.ReadAsync(secure, subject) is not { } tokens) return null;
            if (tokens.AccessTokenExpiresUtc - Skew > clock.GetUtcNow()) return tokens.AccessToken;
            using HttpRequestMessage request = new(HttpMethod.Post, TabletClientContract.TokenPath)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = TabletClientContract.ClientId,
                    ["refresh_token"] = tokens.RefreshToken
                })
            };
            using HttpResponseMessage response = await identity.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                TokenStore.Clear(secure, subject);
                return null;
            }
            response.EnsureSuccessStatusCode();
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            JsonElement root = body.RootElement;
            StoredTokens refreshed = new(root.GetProperty("access_token").GetString()!,
                clock.GetUtcNow().AddSeconds(root.GetProperty("expires_in").GetInt32()),
                root.TryGetProperty("refresh_token", out JsonElement next) ? next.GetString()! : tokens.RefreshToken);
            await TokenStore.SaveAsync(secure, subject, refreshed);
            return refreshed.AccessToken;
        }
        finally { gate.Release(); }
    }
}
