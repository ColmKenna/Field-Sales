using System.Net;
using System.Text.Json;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

public sealed class MemorySecureStore : ISecureStore
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
    public Task<string?> GetAsync(string key) => Task.FromResult(Values.GetValueOrDefault(key));
    public Task SetAsync(string key, string value) { Values[key] = value; return Task.CompletedTask; }
    public bool Remove(string key) => Values.Remove(key);
}

/// <summary>A clock the tests can move either way: the rules must fail closed if the tablet's clock goes back.</summary>
public sealed class MovableClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => UtcNow;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>Stands in for the identity host's token endpoint: refresh tokens rotate on every use (one-time only).</summary>
public sealed class FakeTokenEndpoint : HttpMessageHandler
{
    private int _issued = 1;
    public List<string> RefreshTokensUsed { get; } = [];
    public Func<HttpResponseMessage>? Override { get; set; }

    public HttpClient Client() => new(this) { BaseAddress = new Uri("https://identity.test/") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("/" + TabletClientContract.TokenPath, request.RequestUri!.AbsolutePath);
        Dictionary<string, string> form = (await request.Content!.ReadAsStringAsync(cancellationToken)).Split('&')
            .Select(pair => pair.Split('=')).ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));
        Assert.Equal(("refresh_token", TabletClientContract.ClientId), (form["grant_type"], form["client_id"]));
        RefreshTokensUsed.Add(form["refresh_token"]);
        if (Override is not null) return Override();
        _issued++;
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["access_token"] = $"access-{_issued}", ["expires_in"] = 3600, ["refresh_token"] = $"refresh-{_issued}",
                ["token_type"] = "Bearer"
            }))
        };
    }
}

public sealed class SessionTablet : IDisposable
{
    public static readonly DateTimeOffset Morning = new(2026, 10, 1, 7, 0, 0, TimeSpan.Zero);
    private readonly TestTablet _tablet = new();
    public MovableClock Clock { get; } = new(Morning);
    public MemorySecureStore Secure { get; } = new();
    public FakeTokenEndpoint Identity { get; } = new();
    public FakeSnapshotServer Server => _tablet.Server;
    public TabletDevice Device => new(_tablet.Root, Secure, Clock);

    public static StaffSignIn SignIn(string rep, params string[] roles) =>
        new(rep, roles.Length == 0 ? [TabletClientContract.RequiredRole] : roles, "access-1", Morning.AddHours(1), "refresh-1");

    public TabletSync Sync(string rep) =>
        new(Device.Workspace(rep), Server.Client(), Device.Tokens(rep, Identity.Client()), new TestSpace(), Clock);

    public async Task<SyncOutcome> SyncAsync(string rep, int locations = 3)
    {
        Server.Serve(EncodedSnapshot.From(Snapshots.Build(rep, locations, products: 20, label: rep[..1].ToUpperInvariant())));
        return await Sync(rep).SyncAsync();
    }

    public void Dispose() => _tablet.Dispose();
}
