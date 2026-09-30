using System.Collections.Concurrent;
using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using FieldSales.StaffAccess;
using FieldSales.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FieldSales.Web.Tests;

// Runs the website with its real cookie, ticket store, role policies and pages. Only the
// successful OIDC callback is replaced by a test-only endpoint that issues the same principal.
public sealed class StaffWebsiteFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpMessageHandler? _catalogueHandler;
    private readonly string _accessToken;
    public TestStaffRoleLookup Roles { get; } = new();
    public TestCatalogueHandler Catalogue { get; } = new();

    public StaffWebsiteFactory(HttpMessageHandler? catalogueHandler = null,
        string accessToken = "test-access-token")
    {
        _catalogueHandler = catalogueHandler;
        _accessToken = accessToken;
        _connection.Open();
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "https://localhost:7201",
                ["Authentication:ClientSecret"] = "test-secret",
                ["StaffApi:BaseUrl"] = "https://staff-api.test",
                ["ConnectionStrings:StaffWebDb"] = "Server=localhost;Database=unused;User Id=sa;Password=unused;TrustServerCertificate=True"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<StaffWebDbContext>();
            services.AddScoped(_ => new StaffWebDbContext(
                new DbContextOptionsBuilder<StaffWebDbContext>().UseSqlite(_connection).Options));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddHttpClient(string.Empty)
                .ConfigurePrimaryHttpMessageHandler(() => new TestStaffApiHandler());
            services.AddHttpClient<FieldSales.Web.Catalogue.CatalogueApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => _catalogueHandler ?? Catalogue);
            services.RemoveAll<IStaffRoleLookup>();
            services.AddSingleton(Roles);
            services.AddSingleton<IStaffRoleLookup>(Roles);
            services.AddSingleton<IStartupFilter>(new TestSignInStartupFilter(_accessToken));
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                    new OpenIdConnectConfiguration
                    {
                        Issuer = "https://localhost:7201",
                        AuthorizationEndpoint = "https://localhost:7201/connect/authorize",
                        EndSessionEndpoint = "https://localhost:7201/connect/endsession"
                    });
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);
        using IServiceScope scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<StaffWebDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    private sealed class TestSignInStartupFilter(string accessToken) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuePipeline) =>
            {
                if (context.Request.Path != "/__test/sign-in")
                {
                    await continuePipeline();
                    return;
                }

                string subject = context.Request.Query["subject"].ToString();
                string[] roles = context.Request.Query["roles"].ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (string.IsNullOrWhiteSpace(subject) || roles.Length == 0)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                context.RequestServices.GetRequiredService<TestStaffRoleLookup>().SetRoles(subject, roles);

                Claim[] claims =
                [
                    new("sub", subject),
                    new("name", subject),
                    .. roles.Select(role => new Claim("role", role))
                ];
                ClaimsPrincipal user = new(new ClaimsIdentity(claims,
                    CookieAuthenticationDefaults.AuthenticationScheme, "name", "role"));
                AuthenticationProperties properties = new()
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
                };
                properties.StoreTokens(
                [
                    new AuthenticationToken
                    {
                        Name = "access_token",
                        Value = accessToken
                    },
                    new AuthenticationToken
                    {
                        Name = "expires_at",
                        Value = DateTimeOffset.UtcNow.AddHours(1).ToString("o")
                    }
                ]);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user, properties);
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            });
            next(app);
        };
    }
}

public sealed class TestCatalogueHandler : HttpMessageHandler
{
    private readonly Dictionary<Guid, FieldSales.Web.Catalogue.CategoryItem> _categories = [];

    public int Count { get { lock (_categories) return _categories.Count; } }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-access-token", request.Headers.Authorization?.Parameter);
        string path = request.RequestUri!.AbsolutePath.TrimEnd('/');
        if (path == "/catalogue/categories" && request.Method == HttpMethod.Get)
        {
            lock (_categories)
                return Json(HttpStatusCode.OK, _categories.Values.Where(c => c.ParentId is null)
                    .OrderBy(c => c.Name).ToArray());
        }
        if (path == "/catalogue/categories" && request.Method == HttpMethod.Post)
        {
            var body = await request.Content!.ReadFromJsonAsync<CreateRequest>(cancellationToken);
            lock (_categories)
            {
                if (body!.ParentId is not null && !_categories.ContainsKey(body.ParentId.Value))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                if (_categories.Values.Any(c => c.ParentId == body.ParentId &&
                    string.Equals(c.Name, body.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return new HttpResponseMessage(HttpStatusCode.Conflict);
                FieldSales.Web.Catalogue.CategoryItem added = new(Guid.NewGuid(), body.ParentId, body.Name.Trim());
                _categories.Add(added.Id, added);
                return Json(HttpStatusCode.Created, added);
            }
        }
        if (path.StartsWith("/catalogue/categories/", StringComparison.Ordinal)
            && path.EndsWith("/name", StringComparison.Ordinal)
            && Guid.TryParse(path["/catalogue/categories/".Length..^"/name".Length], out Guid renameId)
            && request.Method == HttpMethod.Put)
        {
            var body = await request.Content!.ReadFromJsonAsync<RenameRequest>(cancellationToken);
            lock (_categories)
            {
                if (!_categories.TryGetValue(renameId, out var selected))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                if (_categories.Values.Any(c => c.Id != renameId && c.ParentId == selected.ParentId
                    && string.Equals(c.Name, body!.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return new HttpResponseMessage(HttpStatusCode.Conflict);
                var renamed = selected with { Name = body!.Name.Trim() };
                _categories[renameId] = renamed;
                return Json(HttpStatusCode.OK, renamed);
            }
        }
        if (path.StartsWith("/catalogue/categories/", StringComparison.Ordinal)
            && Guid.TryParse(path["/catalogue/categories/".Length..], out Guid id)
            && request.Method == HttpMethod.Get)
        {
            lock (_categories)
            {
                if (!_categories.TryGetValue(id, out var selected))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                List<FieldSales.Web.Catalogue.CategoryBreadcrumbSegment> pathSegments = [];
                var current = selected;
                while (true)
                {
                    pathSegments.Add(new(current.Id, current.Name));
                    if (current.ParentId is null) break;
                    current = _categories[current.ParentId.Value];
                }
                pathSegments.Reverse();
                return Json(HttpStatusCode.OK, new FieldSales.Web.Catalogue.CategoryDetails(selected,
                    pathSegments, _categories.Values.Where(c => c.ParentId == id).ToArray(), []));
            }
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T value) =>
        new(status) { Content = JsonContent.Create(value) };

    private sealed record CreateRequest(string Name, Guid? ParentId);
    private sealed record RenameRequest(string Name);
}

internal sealed class TestStaffApiHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
}

public sealed class TestStaffRoleLookup : IStaffRoleLookup
{
    private readonly ConcurrentDictionary<string, string[]> _roles = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public void SetRoles(string subject, params string[] roles) => _roles[subject] = roles;

    public Task<StaffRoleLookupResult> GetRolesAsync(
        string accessToken, string subject, CancellationToken cancellationToken)
    {
        if (Unavailable) throw new HttpRequestException("Test identity host is unavailable.");
        return Task.FromResult(_roles.TryGetValue(subject, out string[]? roles)
            ? StaffRoleLookupResult.Found(roles)
            : StaffRoleLookupResult.Rejected);
    }
}
