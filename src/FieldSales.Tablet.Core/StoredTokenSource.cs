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
internal sealed class StoredTokenSource(ISecureStore secure, string subject, HttpClient identity, TimeProvider clock, long version) : IAccessTokenSource
{
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);
    private readonly TabletSessionState session = TabletSessionState.For(secure);

    // Called under the session gate, both before the request and before accepting its result.
    private async Task<bool> IsCurrentAsync() => !session.Blocked && session.Version == version
        && await secure.GetAsync(TabletDevice.CurrentRepKey) == subject
        && LocalAccessRules.Evaluate(await secure.GetAsync(TabletDevice.SessionKey(subject)) is { } stamp
            ? DateTimeOffset.Parse(stamp, System.Globalization.CultureInfo.InvariantCulture) : null,
            clock.GetUtcNow()) == LocalAccess.Allowed;

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        SemaphoreSlim gate = session.RefreshGates.GetOrAdd(subject, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            StoredTokens? tokens = await session.RunAsync(async () =>
                await IsCurrentAsync() ? await TokenStore.ReadAsync(secure, subject) : null);
            if (tokens is null) return null;
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
                await session.RunAsync(async () =>
                {
                    if (await IsCurrentAsync()) TokenStore.Clear(secure, subject);
                    return true;
                });
                return null;
            }
            response.EnsureSuccessStatusCode();
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            JsonElement root = body.RootElement;
            StoredTokens refreshed = new(root.GetProperty("access_token").GetString()!,
                clock.GetUtcNow().AddSeconds(root.GetProperty("expires_in").GetInt32()),
                root.TryGetProperty("refresh_token", out JsonElement next) ? next.GetString()! : tokens.RefreshToken);
            return await session.RunAsync(async () =>
            {
                if (!await IsCurrentAsync()) return null;
                await TokenStore.SaveAsync(secure, subject, refreshed);
                return refreshed.AccessToken;
            });
        }
        finally { gate.Release(); }
    }
}
