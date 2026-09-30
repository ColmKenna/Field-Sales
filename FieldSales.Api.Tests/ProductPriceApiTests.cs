using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FieldSales.Api.Catalogue;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MsSql;

namespace FieldSales.Api.Tests;

public sealed class ProductPriceApiTests : IClassFixture<ProductPriceApplication>
{
    private readonly ProductPriceApplication _app;
    public ProductPriceApiTests(ProductPriceApplication app) => _app = app;

    [Fact]
    public async Task Should_PreserveHistory_When_ApplicationRestarts()
    {
        Guid id = await _app.SeedAsync(new DateOnly(2026, 9, 30));
        using (HttpClient client = _app.CreateClient())
        {
            foreach (var entry in new[] { (13.20m, new DateOnly(2026, 11, 1)),
                         (14m, new DateOnly(2026, 12, 1)), (12m, new DateOnly(2026, 9, 1)) })
            {
                using HttpResponseMessage response = await AddAsync(client, id, entry.Item1, entry.Item2);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                Assert.Equal($"/catalogue/products/{id}", response.Headers.Location!.OriginalString);
                Assert.Equal(new ProductPriceItem(entry.Item2, entry.Item1),
                    await response.Content.ReadFromJsonAsync<ProductPriceItem>());
            }
        }
        await _app.RestartAsync();
        using HttpClient restarted = _app.CreateClient();
        ProductDetails details = (await restarted.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{id}"))!;
        Assert.Equal(12.50m, details.CurrentPrice!.Amount);
        Assert.Equal(new[] { new DateOnly(2026, 12, 1), new DateOnly(2026, 11, 1),
            new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1) },
            details.PriceHistory.Select(price => price.EffectiveFrom));

        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        Product persisted = await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>()
            .Products.Include(product => product.BasePrices).SingleAsync(product => product.Id == id);
        Assert.Equal(12m, persisted.BasePriceOn(new DateOnly(2026, 9, 29))!.Amount);
        Assert.Equal(12.50m, persisted.BasePriceOn(new DateOnly(2026, 10, 31))!.Amount);
        Assert.Equal(13.20m, persisted.BasePriceOn(new DateOnly(2026, 11, 1))!.Amount);
        Assert.Equal(14m, persisted.BasePriceOn(new DateOnly(2026, 12, 1))!.Amount);
        Assert.Equal(new DateOnly(2026, 9, 29), persisted.BasePricePeriods()[0].EffectiveThrough);
    }

    [Fact]
    public async Task Should_UseNewPrice_When_EffectiveDateIsToday()
    {
        Guid id = await _app.SeedAsync(new DateOnly(2026, 9, 1));
        using HttpClient client = _app.CreateClient();
        // The fixed clock is 30 September 23:30 UTC, already 1 October in Dublin.
        using HttpResponseMessage response = await AddAsync(client, id, 13.20m, new DateOnly(2026, 10, 1));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ProductDetails details = (await client.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{id}"))!;
        Assert.Equal(new ProductPriceItem(new DateOnly(2026, 10, 1), 13.20m), details.CurrentPrice);
        Assert.Equal(2, details.PriceHistory.Count);
    }

    [Fact]
    public async Task Should_RejectDuplicateDate_When_PriceAlreadyStartsThatDay()
    {
        Guid id = await _app.SeedAsync(new DateOnly(2026, 9, 1));
        DateOnly change = new(2026, 11, 1);
        using HttpClient client = _app.CreateClient();
        using HttpResponseMessage first = await AddAsync(client, id, 13.20m, change);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using HttpResponseMessage duplicate = await AddAsync(client, id, 99m, change);
        await AssertDuplicateAsync(duplicate);
        ProductDetails details = (await client.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{id}"))!;
        Assert.Equal(12.50m, details.CurrentPrice!.Amount);
        Assert.Equal(2, details.PriceHistory.Count);
        Assert.Equal(new ProductPriceItem(change, 13.20m), details.PriceHistory[0]);

        // Both request scopes compete for the existing product/date primary key.
        Guid competingId = await _app.SeedAsync(new DateOnly(2026, 9, 1));
        _app.ConcurrentSaves.ProductId = competingId;
        HttpResponseMessage[] responses = await Task.WhenAll(
            AddAsync(client, competingId, 13.20m, change), AddAsync(client, competingId, 14m, change));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            await AssertDuplicateAsync(Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict));
            ProductDetails winner = (await client.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{competingId}"))!;
            Assert.Equal(2, winner.PriceHistory.Count);
            ProductPriceItem saved = (await Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created)
                .Content.ReadFromJsonAsync<ProductPriceItem>())!;
            Assert.Equal(saved, winner.PriceHistory[0]);
        }
        finally { foreach (HttpResponseMessage response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Should_PreserveHistory_When_AddRequestIsInvalid()
    {
        Guid id = await _app.SeedAsync(new DateOnly(2026, 9, 1));
        using HttpClient client = _app.CreateClient();
        DateOnly date = new(2026, 11, 1);
        foreach (decimal? amount in new decimal?[] { null, -1m, 12.501m, ProductBasePrice.MaximumAmount + 0.01m })
        {
            using HttpResponseMessage response = await AddAsync(client, id, amount, date);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("BasePrice", await response.Content.ReadAsStringAsync());
        }
        using HttpResponseMessage missingDate = await AddAsync(client, id, 13.20m, null);
        Assert.Equal(HttpStatusCode.BadRequest, missingDate.StatusCode);
        Assert.Contains("Enter an effective from date.", await missingDate.Content.ReadAsStringAsync());
        using HttpResponseMessage malformedDate = await client.PostAsJsonAsync($"/catalogue/products/{id}/base-prices",
            new { BasePrice = 13.20m, EffectiveFrom = "not-a-date" });
        Assert.Equal(HttpStatusCode.BadRequest, malformedDate.StatusCode);
        ProductDetails unchanged = (await client.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{id}"))!;
        Assert.Equal(12.50m, Assert.Single(unchanged.PriceHistory).Amount);

        using HttpResponseMessage missing = await AddAsync(client, Guid.NewGuid(), 13.20m, date);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Should_DenyPriceWrite_When_RoleIsMissingOrTokenExpires()
    {
        Guid id = await _app.SeedAsync(new DateOnly(2026, 9, 1));
        using HttpClient client = _app.CreateClient();
        foreach (string role in new[] { BusinessRoles.FieldSalesperson, BusinessRoles.SalesManager })
        {
            _app.Roles.Current = [role];
            using HttpResponseMessage denied = await AddAsync(client, id, 13.20m, new DateOnly(2026, 11, 1));
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        _app.Roles.Current = [];
        using (HttpResponseMessage removed = await AddAsync(client, id, 13.20m, new DateOnly(2026, 11, 1)))
            Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
        _app.Roles.Current = [BusinessRoles.HeadOfficeUser];
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProductPriceApplication.Token(expired: true));
        using (HttpResponseMessage expired = await AddAsync(client, id, 13.20m, new DateOnly(2026, 11, 1)))
            Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using (HttpResponseMessage anonymous = await AddAsync(client, id, 13.20m, new DateOnly(2026, 11, 1)))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProductPriceApplication.Token());
        ProductDetails unchanged = (await client.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{id}"))!;
        Assert.Equal(12.50m, Assert.Single(unchanged.PriceHistory).Amount);
    }

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid id, decimal? amount, DateOnly? date) =>
        client.PostAsJsonAsync($"/catalogue/products/{id}/base-prices", new AddProductBasePriceRequest(amount, date));

    private static async Task AssertDuplicateAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        ProductSaveError error = (await response.Content.ReadFromJsonAsync<ProductSaveError>())!;
        Assert.Equal("EffectiveFrom", error.Field);
        Assert.Equal("A price already starts on 1 Nov 2026 — edit it instead", error.Error);
    }
}

public sealed class ProductPriceApplication : IAsyncLifetime
{
    private const string Issuer = "https://staff-issuer.test";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("test-only-staff-api-signing-key-32bytes"));
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    public PriceRoleLookup Roles { get; } = new();
    public PriceSaveBarrier ConcurrentSaves { get; } = new();
    public WebApplicationFactory<Program> Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        Api = NewApi();
        _ = Api.Server;
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await _sql.DisposeAsync();
    }

    public async Task RestartAsync()
    {
        await Api.DisposeAsync();
        Api = NewApi();
    }

    public HttpClient CreateClient()
    {
        HttpClient client = Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        return client;
    }

    public async Task<Guid> SeedAsync(DateOnly effectiveFrom)
    {
        Roles.Current = [BusinessRoles.HeadOfficeUser];
        await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Category category = new CategoryTree([]).Add(Guid.NewGuid().ToString());
        Product product = Product.Create(Guid.NewGuid().ToString(), "Lotion", category.Id, 12.50m, effectiveFrom);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private WebApplicationFactory<Program> NewApi() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = Issuer,
            ["ConnectionStrings:CatalogueDb"] = _sql.GetConnectionString()
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<CatalogueDbContext>();
            services.AddScoped(_ => new CatalogueDbContext(new DbContextOptionsBuilder<CatalogueDbContext>()
                .UseSqlServer(_sql.GetConnectionString()).AddInterceptors(ConcurrentSaves).Options));
            services.RemoveAll<IStaffRoleLookup>();
            services.AddSingleton<IStaffRoleLookup>(Roles);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new PriceClock());
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                OpenIdConnectConfiguration configuration = new() { Issuer = Issuer };
                configuration.SigningKeys.Add(SigningKey);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    });

    public static string Token(bool expired = false)
    {
        JwtSecurityToken token = new(issuer: Issuer, audience: "fieldsales-api",
            claims: [new Claim("sub", "staff-1"), new Claim("scope", "fieldsales.api"), new Claim("role", BusinessRoles.HeadOfficeUser)],
            notBefore: DateTime.UtcNow.AddHours(-1), expires: DateTime.UtcNow.AddMinutes(expired ? -10 : 30),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public sealed class PriceRoleLookup : IStaffRoleLookup
    {
        public string[] Current { get; set; } = [BusinessRoles.HeadOfficeUser];
        public Task<StaffRoleLookupResult> GetRolesAsync(string accessToken, string subject, CancellationToken cancellationToken) =>
            Task.FromResult(StaffRoleLookupResult.Found(Current));
    }

    // Hold both requests after their duplicate lookup and before either insert reaches SQL.
    // This exercises the database conflict path rather than relying on request scheduling.
    public sealed class PriceSaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _bothReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public Guid ProductId { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<ProductBasePrice>()
                .Any(entry => entry.State == EntityState.Added && entry.Entity.ProductId == ProductId))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _bothReady.TrySetResult();
                await _bothReady.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }

    private sealed class PriceClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 30, 23, 30, 0, TimeSpan.Zero);
    }
}
