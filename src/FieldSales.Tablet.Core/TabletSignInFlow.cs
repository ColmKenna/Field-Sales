using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>An authorization request waiting for the browser to return; held only in memory.</summary>
public sealed record PendingSignIn(Uri AuthorizeUrl, string State, string CodeVerifier);

/// <summary>The connected sign-in (P7): authorization code with PKCE in the system browser, then the code exchange.</summary>
/// <remarks>
/// <c>prompt=login</c> asks for credentials every time, so on a shared tablet the browser's identity session can
/// never sign the next rep in as the previous one. The rep's identity and current roles come from the API's
/// <c>/staff/session</c> with the new access token, never from the browser.
/// </remarks>
public sealed class TabletSignInFlow(HttpClient identity, HttpClient api, TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public PendingSignIn Start()
    {
        string verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        string state = Base64Url(RandomNumberGenerator.GetBytes(16));
        Dictionary<string, string> query = new()
        {
            ["client_id"] = TabletClientContract.ClientId,
            ["redirect_uri"] = TabletClientContract.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = TabletClientContract.Scopes,
            ["state"] = state,
            ["code_challenge"] = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            ["code_challenge_method"] = "S256",
            ["prompt"] = "login"
        };
        string encoded = string.Join("&", query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
        return new(new Uri(identity.BaseAddress!, "connect/authorize?" + encoded), state, verifier);
    }

    /// <summary>Exchanges the browser's callback for tokens and reads the rep's identity and current roles.</summary>
    /// <returns>Null when the identity host refused the sign-in (the rep is returned to the sign-in screen).</returns>
    public async Task<StaffSignIn?> CompleteAsync(PendingSignIn pending, IReadOnlyDictionary<string, string> callback,
        CancellationToken ct = default)
    {
        if (!callback.TryGetValue("state", out string? state)
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(state), Encoding.ASCII.GetBytes(pending.State)))
            throw new InvalidDataException("The sign-in response does not belong to this request.");
        if (callback.ContainsKey("error") || !callback.TryGetValue("code", out string? code)) return null;

        using HttpRequestMessage exchange = new(HttpMethod.Post, TabletClientContract.TokenPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = TabletClientContract.RedirectUri,
                ["client_id"] = TabletClientContract.ClientId,
                ["code_verifier"] = pending.CodeVerifier
            })
        };
        using HttpResponseMessage tokens = await identity.SendAsync(exchange, ct);
        if (tokens.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized) return null;
        tokens.EnsureSuccessStatusCode();
        using JsonDocument body = JsonDocument.Parse(await tokens.Content.ReadAsStringAsync(ct));
        string accessToken = body.RootElement.GetProperty("access_token").GetString()!;
        DateTimeOffset expires = clock.GetUtcNow().AddSeconds(body.RootElement.GetProperty("expires_in").GetInt32());
        string refreshToken = body.RootElement.TryGetProperty("refresh_token", out JsonElement refresh)
            ? refresh.GetString()! : throw new InvalidDataException("The sign-in did not grant offline access.");

        using HttpRequestMessage who = new(HttpMethod.Get, "staff/session");
        who.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage session = await api.SendAsync(who, ct);
        // Staff with no business role are refused by the API; they get no tablet day, and nothing is kept.
        if (session.StatusCode == HttpStatusCode.Forbidden) return new("", [], accessToken, expires, refreshToken);
        session.EnsureSuccessStatusCode();
        StaffSessionReply reply = JsonSerializer.Deserialize(await session.Content.ReadAsStringAsync(ct), TabletJsonContext.Default.StaffSessionReply)
            ?? throw new InvalidDataException("The staff session reply is empty.");
        return new(reply.Subject, reply.Roles, accessToken, expires, refreshToken);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

internal sealed record StaffSessionReply(string Subject, string[] Roles);

// Explicit metadata for trimmed tablet Release builds.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StaffSessionReply))]
[JsonSerializable(typeof(StoredTokens))]
internal sealed partial class TabletJsonContext : JsonSerializerContext;
