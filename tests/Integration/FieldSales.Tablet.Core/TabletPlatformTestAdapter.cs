// Only the native platform APIs are adapted. TabletApp and its session/Sync
// orchestration are linked production source in the desktop integration project.
namespace FieldSales.Tablet;

public sealed record TabletEndpoints(HttpClient Api, HttpClient Identity);
public enum NetworkAccess { Internet }
public static class Connectivity
{
    public static TestConnectivity Current { get; } = new();
    public sealed class TestConnectivity { public NetworkAccess NetworkAccess => NetworkAccess.Internet; }
}
public sealed class WebAuthenticatorOptions
{
    public Uri Url { get; set; } = null!;
    public Uri CallbackUrl { get; set; } = null!;
}
public sealed class WebAuthenticatorResult
{
    public IReadOnlyDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}
public static class WebAuthenticator
{
    public static TestAuthenticator Default { get; } = new();
    public sealed class TestAuthenticator
    {
        public Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions options) =>
            throw new NotSupportedException("Desktop scenarios do not drive the Android system browser.");
    }
}
