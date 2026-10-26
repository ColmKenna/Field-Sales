using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core.Tests;

// WI-032 increment 4a (P7): the system-browser sign-in. The browser itself is the tablet's; these tests begin
// with the authorization request it is given and the callback it returns.
public sealed class TabletSignInFlowTests : IDisposable
{
    private readonly SessionTablet _tablet = new();
    private readonly SignInServer _identity = new();
    private readonly SignInServer _api = new();

    public void Dispose() => _tablet.Dispose();

    private TabletSignInFlow Flow() => new(_identity.Client("https://identity.test/"), _api.Client("https://api.test/"), _tablet.Clock);

    [Fact]
    public void Should_RequestACodeWithPkceAndAFreshLogin_When_SignInStarts()
    {
        PendingSignIn pending = Flow().Start();
        Assert.Equal("https://identity.test/connect/authorize", pending.AuthorizeUrl.GetLeftPart(UriPartial.Path));
        Dictionary<string, string> query = Query(pending.AuthorizeUrl.Query);
        Assert.Equal(TabletClientContract.ClientId, query["client_id"]);
        Assert.Equal(TabletClientContract.RedirectUri, query["redirect_uri"]);
        Assert.Equal(("code", TabletClientContract.Scopes, "login"), (query["response_type"], query["scope"], query["prompt"]));
        Assert.Equal((pending.State, "S256"), (query["state"], query["code_challenge_method"]));
        Assert.Equal(Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(pending.CodeVerifier))), query["code_challenge"]);
        PendingSignIn another = Flow().Start();
        Assert.NotEqual((pending.State, pending.CodeVerifier), (another.State, another.CodeVerifier));
    }

    [Fact]
    public async Task Should_StartTheDay_When_TheBrowserReturnsTheCode()
    {
        _identity.Respond = request => Json(new Dictionary<string, object>
            { ["access_token"] = "access-1", ["expires_in"] = 3600, ["refresh_token"] = "refresh-1", ["id_token"] = "ignored" });
        _api.Respond = _ => Json(new Dictionary<string, object> { ["subject"] = "colm", ["roles"] = new[] { "Field Salesperson" } });
        TabletSignInFlow flow = Flow();
        PendingSignIn pending = flow.Start();

        StaffSignIn signIn = (await flow.CompleteAsync(pending, new Dictionary<string, string> { ["code"] = "abc", ["state"] = pending.State }))!;
        Dictionary<string, string> exchange = Query(_identity.Bodies.Single());
        Assert.Equal(("authorization_code", "abc", pending.CodeVerifier), (exchange["grant_type"], exchange["code"], exchange["code_verifier"]));
        Assert.Equal((TabletClientContract.RedirectUri, TabletClientContract.ClientId), (exchange["redirect_uri"], exchange["client_id"]));
        // Identity and roles come from the API with the new token, not from the browser.
        Assert.Equal(("/staff/session", "Bearer access-1"), (_api.Paths.Single(), _api.Authorizations.Single()));
        Assert.Equal(("colm", "refresh-1", _tablet.Clock.UtcNow.AddHours(1)), (signIn.Subject, signIn.RefreshToken, signIn.AccessTokenExpiresUtc));

        Assert.Equal(SignInStatus.Started, (await _tablet.Device.CompleteSignInAsync(signIn)).Status);
        Assert.Equal((EntryScreen.Sync, "colm"), ((await _tablet.Device.EnterAsync(connected: true)) is var entry ? (entry.Screen, entry.RepSubject) : default));
    }

    [Fact]
    public async Task Should_RefuseTheResponse_When_ItIsNotForThisRequest()
    {
        TabletSignInFlow flow = Flow();
        PendingSignIn pending = flow.Start();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            flow.CompleteAsync(pending, new Dictionary<string, string> { ["code"] = "abc", ["state"] = "someone-else" }));
        await Assert.ThrowsAsync<InvalidDataException>(() => flow.CompleteAsync(pending, new Dictionary<string, string> { ["code"] = "abc" }));
        Assert.Empty(_identity.Bodies);
    }

    [Fact]
    public async Task Should_ReturnToSignIn_When_TheIdentityHostRefuses()
    {
        TabletSignInFlow flow = Flow();
        PendingSignIn pending = flow.Start();
        Assert.Null(await flow.CompleteAsync(pending, new Dictionary<string, string> { ["error"] = "access_denied", ["state"] = pending.State }));
        _identity.Respond = _ => new(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"invalid_grant"}""") };
        Assert.Null(await flow.CompleteAsync(pending, new Dictionary<string, string> { ["code"] = "expired", ["state"] = pending.State }));
        Assert.Empty(_api.Paths);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, null)]
    [InlineData(HttpStatusCode.OK, "Sales Manager")]
    public async Task Should_RefuseTheTabletDay_When_TheApiShowsNoFieldSalespersonRole(HttpStatusCode status, string? role)
    {
        _identity.Respond = _ => Json(new Dictionary<string, object> { ["access_token"] = "access-1", ["expires_in"] = 3600, ["refresh_token"] = "refresh-1" });
        _api.Respond = _ => status == HttpStatusCode.OK
            ? Json(new Dictionary<string, object> { ["subject"] = "niamh", ["roles"] = new[] { role! } })
            : new(status);
        TabletSignInFlow flow = Flow();
        PendingSignIn pending = flow.Start();
        StaffSignIn signIn = (await flow.CompleteAsync(pending, new Dictionary<string, string> { ["code"] = "abc", ["state"] = pending.State }))!;

        SignInOutcome outcome = await _tablet.Device.CompleteSignInAsync(signIn);
        Assert.Equal((SignInStatus.NotPermitted, "This app is for Field Salespeople."), (outcome.Status, outcome.Message));
        Assert.Empty(_tablet.Secure.Values);
    }

    private static Dictionary<string, string> Query(string encoded) => encoded.TrimStart('?').Split('&')
        .Select(pair => pair.Split('=')).ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)) };

    private sealed class SignInServer : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new(HttpStatusCode.ServiceUnavailable);
        public List<string> Bodies { get; } = [];
        public List<string> Paths { get; } = [];
        public List<string?> Authorizations { get; } = [];
        public HttpClient Client(string baseAddress) => new(this) { BaseAddress = new Uri(baseAddress) };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Authorizations.Add(request.Headers.Authorization?.ToString());
            if (request.Content is not null) Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            return Respond(request);
        }
    }
}
