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
public sealed class ProductAttributeApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly ProductPriceApplication app = new(fixture.CreateConnectionString("AttributeApi"));
    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => app.DisposeAsync();

    [Fact]
    public async Task Should_PreserveOrderedExactValues_When_AttributesAreAddedAndTheServerRestarts()
    {
        var (product, name) = await SeedAsync();
        string longValue = "  Quoted \"value\" ·\n" + new string('界', 6000);
        string[] values = ["200 g", "", longValue];
        using (HttpClient api = app.CreateClient())
            foreach (string value in values)
            {
                using var response = await api.PostAsJsonAsync(Path(product), new AddProductAttributeRequest(name.Id, value));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            }
        await app.RestartAsync();
        using HttpClient reopened = app.CreateClient();
        ProductDetails details = (await reopened.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{product}"))!;
        Assert.Equal(values, details.Attributes.Select(row => row.Value));
        Assert.All(details.Attributes, row => Assert.Equal("Weight", row.Name));
        Assert.Equal(12.50m, details.CurrentPrice!.Amount);
        Assert.Equal("Each", details.Product.Unit);
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        int[] positions = await db.ProductAttributeValues.OrderBy(row => row.Position).Select(row => row.Position).ToArrayAsync();
        Assert.Equal([0, 1, 2], positions);
        Assert.Equal(1, (await new ProductReferenceUsageSource(db).CountAsync(new("attribute-names", name.Id), default)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RejectStaleNameWithoutChangingValues_When_NameIsArchivedOrDeleted(bool deleted)
    {
        var (product, name) = await SeedAsync();
        using HttpClient api = app.CreateClient();
        Assert.Contains((await DetailsAsync(api, product)).AttributeNameChoices!, item => item.Id == name.Id);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            var saved = await db.AttributeNames.SingleAsync();
            if (deleted) db.Remove(saved); else saved.Archive();
            await db.SaveChangesAsync();
        }
        using var response = await api.PostAsJsonAsync(Path(product), new AddProductAttributeRequest(name.Id, "200 g"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("NameId", (await response.Content.ReadFromJsonAsync<ProductValidationErrors>())!.Errors.Keys);
        var details = await DetailsAsync(api, product);
        Assert.Empty(details.Attributes);
        Assert.DoesNotContain(details.AttributeNameChoices!, item => item.Id == name.Id);
    }

    [Fact]
    public async Task Should_KeepRenamedArchivedValuesAndExcludeTheirChoices_When_NameIsRetired()
    {
        var (product, name) = await SeedAsync();
        using HttpClient api = app.CreateClient();
        using var added = await api.PostAsJsonAsync(Path(product), new AddProductAttributeRequest(name.Id, "200 g"));
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        await using (var scope = app.Api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            var saved = await db.AttributeNames.SingleAsync();
            saved.Rename("Mass"); saved.Archive();
            await db.SaveChangesAsync();
        }
        using var rejected = await api.PostAsJsonAsync(Path(product), new AddProductAttributeRequest(name.Id, "300 g"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var details = await DetailsAsync(api, product);
        Assert.Equal(new FieldSales.Catalogue.Contracts.ProductAttribute("Mass", "200 g", true), Assert.Single(details.Attributes));
        Assert.Empty(details.AttributeNameChoices!);
    }

    [Fact]
    public async Task Should_RejectInvalidInputsWithoutWriting_When_NameOrValueIsMissing()
    {
        var (product, name) = await SeedAsync();
        using HttpClient api = app.CreateClient();
        foreach (var request in new[] { new AddProductAttributeRequest(null, "200 g"), new(Guid.Empty, "200 g"),
                     new(Guid.NewGuid(), "200 g"), new(name.Id, null) })
        {
            using var response = await api.PostAsJsonAsync(Path(product), request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var missing = await api.PostAsJsonAsync(Path(Guid.NewGuid()), new AddProductAttributeRequest(name.Id, "200 g"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty((await DetailsAsync(api, product)).Attributes);
    }

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData(BusinessRoles.FieldSalesperson, HttpStatusCode.Forbidden)]
    [InlineData(BusinessRoles.SalesManager, HttpStatusCode.Forbidden)]
    public async Task Should_RejectDirectApiWrites_When_CurrentAccessIsMissing(string access, HttpStatusCode expected)
    {
        var (product, name) = await SeedAsync();
        using HttpClient api = app.CreateClient();
        if (access == "anonymous") api.DefaultRequestHeaders.Authorization = null;
        else if (access == "expired") api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProductPriceApplication.Token(expired: true));
        else app.Roles.Current = [access];
        using var denied = await api.PostAsJsonAsync(Path(product), new AddProductAttributeRequest(name.Id, "200 g"));
        Assert.Equal(expected, denied.StatusCode);
        await using var scope = app.Api.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().ProductAttributeValues.ToArrayAsync());
    }

    private async Task<(Guid Product, AttributeName Name)> SeedAsync()
    {
        Guid product = await app.SeedAsync(new DateOnly(2026, 8, 1));
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        AttributeName name = AttributeName.Create("Weight");
        db.AttributeNames.Add(name);
        await db.SaveChangesAsync();
        return (product, name);
    }
    private static string Path(Guid id) => $"/catalogue/products/{id}/attributes";
    private static async Task<ProductDetails> DetailsAsync(HttpClient api, Guid id) =>
        (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{id}"))!;
}
