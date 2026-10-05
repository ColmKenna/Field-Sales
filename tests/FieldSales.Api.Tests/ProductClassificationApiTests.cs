using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Catalogue.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Api.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class ProductClassificationApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly ProductPriceApplication app = new(fixture.CreateConnectionString("ClassificationApi"));

    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => app.DisposeAsync();
    [Fact]
    public async Task Should_SaveAllClassificationAndPreserveRecord_When_ApplicationRestarts()
    {
        Seed seed = await SeedAsync();
        using (HttpClient api = app.CreateClient())
        {
            using var saved = await SaveAsync(api, seed.ProductId, Request(seed));
            Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        }
        await app.RestartAsync();
        using HttpClient reopened = app.CreateClient();
        ProductDetails details = (await reopened.GetFromJsonAsync<ProductDetails>(Path(seed.ProductId)))!;
        Assert.Equal(seed.Profile.Id, details.Profile!.Id);
        Assert.Equal(seed.Supplier.Id, details.Supplier!.Id);
        Assert.Equal(seed.Group.Id, details.RestrictionGroup!.Id);
        Assert.Equal(seed.Primary.Id, Assert.Single(details.Brands!, brand => brand.IsPrimary).Id);
        Assert.Equal(seed.Alternative.Id, Assert.Single(details.Brands!, brand => brand.IsAlternative).Id);
        Assert.Equal(12.50m, details.CurrentPrice!.Amount);
        Assert.Single(details.PriceHistory);
        Assert.Equal("Each", details.Product.Unit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RejectWholeSave_When_AlternativesHaveNoPrimary(bool previouslyAssigned)
    {
        Seed seed = await SeedAsync(previouslyAssigned);
        using HttpClient api = app.CreateClient();
        using var invalid = await SaveAsync(api, seed.ProductId, Request(seed) with { PrimaryBrandId = null });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("Choose a primary brand first", Assert.Single(
            (await invalid.Content.ReadFromJsonAsync<ProductValidationErrors>())!.Errors["PrimaryBrandId"]));
        var details = (await api.GetFromJsonAsync<ProductDetails>(Path(seed.ProductId)))!;
        Assert.Equal(previouslyAssigned ? seed.Profile.Id : (Guid?)null, details.Profile?.Id);
        Assert.Equal(previouslyAssigned ? seed.Group.Id : (Guid?)null, details.RestrictionGroup?.Id);
        Assert.Equal(previouslyAssigned ? seed.Primary.Id : (Guid?)null,
            details.Brands!.SingleOrDefault(brand => brand.IsPrimary)?.Id);
    }

    [Fact]
    public async Task Should_CountProductOnce_When_BrandHasBothRoles()
    {
        Seed seed = await SeedAsync();
        using HttpClient api = app.CreateClient();
        using var saved = await SaveAsync(api, seed.ProductId,
            Request(seed) with { AlternativeBrandIds = [seed.Primary.Id] });
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        var details = (await api.GetFromJsonAsync<ProductDetails>(Path(seed.ProductId)))!;
        var brand = Assert.Single(details.Brands!);
        Assert.True(brand.IsPrimary);
        Assert.True(brand.IsAlternative);
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Assert.Equal(1, (await new ProductBrandUsageSource(db).CountAsync(new("brands", seed.Primary.Id), default)).Count);
    }

    [Fact]
    public async Task Should_RejectWholeSave_When_AlternativeIdsAreRepeatedOrReferencesInvalid()
    {
        Seed seed = await SeedAsync();
        using HttpClient api = app.CreateClient();
        foreach (var request in new[]
        {
            Request(seed) with { AlternativeBrandIds = [seed.Alternative.Id, seed.Alternative.Id] },
            Request(seed) with { ProfileId = Guid.NewGuid() },
            Request(seed) with { PrimaryBrandId = Guid.NewGuid() },
            Request(seed) with { AlternativeBrandIds = [Guid.NewGuid()] },
            Request(seed) with { SupplierId = Guid.Empty },
            Request(seed) with { RestrictionGroupId = Guid.NewGuid() }
        })
        {
            using var rejected = await SaveAsync(api, seed.ProductId, request);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            var details = (await api.GetFromJsonAsync<ProductDetails>(Path(seed.ProductId)))!;
            Assert.Null(details.Profile);
            Assert.Null(details.Supplier);
            Assert.Null(details.RestrictionGroup);
            Assert.Empty(details.Brands!);
        }
        using var missing = await SaveAsync(api, Guid.NewGuid(), Request(seed));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("profiles", "ProfileId")]
    [InlineData("brands", "PrimaryBrandId")]
    [InlineData("suppliers", "SupplierId")]
    [InlineData("restriction-groups", "RestrictionGroupId")]
    public async Task Should_RejectStaleSelectionAndKeepChoicesActive_When_ReferenceIsArchived(string key, string field)
    {
        Seed seed = await SeedAsync();
        using HttpClient api = app.CreateClient();
        var before = (await api.GetFromJsonAsync<ProductDetails>(Path(seed.ProductId)))!;
        Assert.NotNull(before.ClassificationChoices);
        NamedReferenceItem item = key switch
        {
            "profiles" => seed.Profile, "brands" => seed.Primary,
            "suppliers" => seed.Supplier, _ => seed.Group
        };
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            db.Attach(item);
            item.Archive();
            await db.SaveChangesAsync();
        }
        using var rejected = await SaveAsync(api, seed.ProductId, Request(seed));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains(field, (await rejected.Content.ReadFromJsonAsync<ProductValidationErrors>())!.Errors.Keys);
        var after = (await api.GetFromJsonAsync<ProductDetails>(Path(seed.ProductId)))!;
        Assert.Null(after.Profile);
        Assert.Empty(after.Brands!);
        Assert.Null(after.Supplier);
        Assert.Null(after.RestrictionGroup);
        var choices = key switch
        {
            "profiles" => after.ClassificationChoices!.Profiles, "brands" => after.ClassificationChoices!.Brands,
            "suppliers" => after.ClassificationChoices!.Suppliers, _ => after.ClassificationChoices!.RestrictionGroups
        };
        Assert.DoesNotContain(choices, choice => choice.Id == item.Id);
    }

    [Fact]
    public async Task Should_RetainAndClearArchivedReferences_When_ExistingLinksAreUnchanged()
    {
        Seed seed = await SeedAsync(assigned: true);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            foreach (NamedReferenceItem item in new NamedReferenceItem[] { seed.Profile, seed.Primary, seed.Alternative, seed.Supplier, seed.Group })
            {
                db.Attach(item);
                item.Archive();
            }
            await db.SaveChangesAsync();
        }
        using HttpClient api = app.CreateClient();
        using var retained = await SaveAsync(api, seed.ProductId, Request(seed));
        Assert.Equal(HttpStatusCode.NoContent, retained.StatusCode);
        var details = (await api.GetFromJsonAsync<ProductDetails>(Path(seed.ProductId)))!;
        Assert.True(details.Profile!.IsArchived);
        Assert.True(details.Supplier!.IsArchived);
        Assert.True(details.RestrictionGroup!.IsArchived);
        Assert.All(details.Brands!, brand => Assert.True(brand.IsArchived));
        using var cleared = await SaveAsync(api, seed.ProductId, new(null, null, [], null, null));
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        details = (await api.GetFromJsonAsync<ProductDetails>(Path(seed.ProductId)))!;
        Assert.Null(details.Profile);
        Assert.Null(details.Supplier);
        Assert.Null(details.RestrictionGroup);
        Assert.Empty(details.Brands!);
    }

    [Fact]
    public async Task Should_UpdateUsageCounts_When_ClassificationIsReplacedOrCleared()
    {
        Seed seed = await SeedAsync(assigned: true);
        using HttpClient api = app.CreateClient();
        RestrictionGroup replacement = RestrictionGroup.Create("Replacement " + Guid.NewGuid());
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            db.RestrictionGroups.Add(replacement);
            await db.SaveChangesAsync();
        }
        using var replaced = await SaveAsync(api, seed.ProductId, Request(seed) with { RestrictionGroupId = replacement.Id });
        Assert.Equal(HttpStatusCode.NoContent, replaced.StatusCode);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<FieldSales.ReferenceData.IReferenceUsageReader>();
            Assert.Equal(0, (await reader.ReadAsync(new("restriction-groups", seed.Group.Id), default)).Counts.Single().Count);
            Assert.Equal(1, (await reader.ReadAsync(new("restriction-groups", replacement.Id), default)).Counts.Single().Count);
            foreach (var item in new[] { ("profiles", seed.Profile.Id), ("suppliers", seed.Supplier.Id), ("brands", seed.Primary.Id), ("brands", seed.Alternative.Id) })
                Assert.Equal(1, (await reader.ReadAsync(new(item.Item1, item.Item2), default)).Counts.Single().Count);
        }
        using var cleared = await SaveAsync(api, seed.ProductId, new(null, null, [], null, null));
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        await using var clearedScope = app.Api.Services.CreateAsyncScope();
        var clearedReader = clearedScope.ServiceProvider.GetRequiredService<FieldSales.ReferenceData.IReferenceUsageReader>();
        foreach (var item in new[] { ("profiles", seed.Profile.Id), ("suppliers", seed.Supplier.Id), ("brands", seed.Primary.Id), ("brands", seed.Alternative.Id), ("restriction-groups", replacement.Id) })
            Assert.Equal(0, (await clearedReader.ReadAsync(new(item.Item1, item.Item2), default)).Counts.Single().Count);
    }

    [Fact]
    public async Task Should_DenyWrite_When_RoleIsRemovedOrTokenExpires()
    {
        Seed seed = await SeedAsync();
        using HttpClient api = app.CreateClient();
        app.Roles.Current = [BusinessRoles.SalesManager];
        using var denied = await SaveAsync(api, seed.ProductId, Request(seed));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        app.Roles.Current = [BusinessRoles.HeadOfficeUser];
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProductPriceApplication.Token(expired: true));
        using var expired = await SaveAsync(api, seed.ProductId, Request(seed));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        await using var scope = app.Api.Services.CreateAsyncScope();
        var product = await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Products.SingleAsync(p => p.Id == seed.ProductId);
        Assert.Null(product.ProductProfileId);
        Assert.Null(product.PrimaryBrandId);
        Assert.Null(product.SupplierId);
        Assert.Null(product.RestrictionGroupId);
    }

    private async Task<Seed> SeedAsync(bool assigned = false)
    {
        Guid id = await app.SeedAsync(new DateOnly(2026, 8, 1));
        string suffix = " " + Guid.NewGuid();
        Seed seed = new(id, ProductProfile.Create("Chilled" + suffix), Brand.Create("SunCo" + suffix),
            Brand.Create("GlowCo" + suffix), Supplier.Create("Irish Health Supplies" + suffix),
            RestrictionGroup.Create("Pharmacy-only medicines" + suffix));
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        db.AddRange(seed.Profile, seed.Primary, seed.Alternative, seed.Supplier, seed.Group);
        if (assigned)
        {
            var product = await db.Products.SingleAsync(p => p.Id == id);
            product.SetClassification(seed.Profile, seed.Supplier);
            product.SetBrands(seed.Primary, [seed.Alternative]);
            product.SetRestrictionGroup(seed.Group);
        }
        await db.SaveChangesAsync();
        return seed;
    }

    private static SetProductClassificationRequest Request(Seed seed) => new(seed.Profile.Id,
        seed.Primary.Id, [seed.Alternative.Id], seed.Supplier.Id, seed.Group.Id);
    private static string Path(Guid id) => $"/catalogue/products/{id}";
    private static Task<HttpResponseMessage> SaveAsync(HttpClient api, Guid id, SetProductClassificationRequest request) =>
        api.PutAsJsonAsync(Path(id) + "/classification", request);
    private sealed record Seed(Guid ProductId, ProductProfile Profile, Brand Primary, Brand Alternative,
        Supplier Supplier, RestrictionGroup Group);
}
