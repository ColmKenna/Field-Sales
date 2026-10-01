extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.ReferenceData;
using FieldSales.Web.Catalogue;
using FieldSales.Web.Data;
using FieldSales.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;
using NamedReferenceItem = CatalogueApi::FieldSales.Api.Catalogue.NamedReferenceItem;
using ProductReferenceAssignments = CatalogueApi::FieldSales.Api.Catalogue.ProductReferenceAssignments;

namespace FieldSales.Web.Tests;

public sealed class ReferenceListEndToEndTests(ProductApplication app) : IClassFixture<ProductApplication>
{
    private const string Page = "/HeadOffice/ReferenceData";
    private static string ApiPath(string key) => "/catalogue/reference-data/" + key;

    [Theory]
    [InlineData("profiles")]
    [InlineData("attribute-names")]
    [InlineData("suppliers")]
    public async Task Should_SaveAndRenameAcrossRestart_When_ReferenceNameIsValid(string key)
    {
        await ResetAsync();
        await using (var website = app.CreateWebsite())
        using (var browser = website.CreateBrowser())
        {
            await SignInAsync(browser);
            string html = await HtmlAsync(browser, key);
            foreach (string choice in new[] { "brands", "profiles", "attribute-names", "suppliers" })
                Assert.Contains($"value=\"{choice}\"", html);
            using var added = await PostAsync(browser, key, "Save", html, new() { ["Name"] = " Fresh " });
            Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
            Guid id = (await ItemsAsync(key)).Single().Id;
            using var renamed = await PostAsync(browser, key, "Save", await HtmlAsync(browser, key), new()
                { ["Id"] = id.ToString(), ["Name"] = " Renamed " });
            Assert.Equal(HttpStatusCode.Redirect, renamed.StatusCode);
        }
        await using var restartedApi = app.NewApi();
        await using var restartedWebsite = app.CreateWebsite(restartedApi);
        using var reopened = restartedWebsite.CreateBrowser();
        await SignInAsync(reopened);
        Assert.Contains("Renamed", await HtmlAsync(reopened, key));
        Assert.Equal("Renamed", (await ItemsAsync(key)).Single().Name);
    }

    [Theory]
    [InlineData("profiles")]
    [InlineData("attribute-names")]
    [InlineData("suppliers")]
    public async Task Should_RejectNames_When_InvalidOrDuplicateIncludingArchived(string key)
    {
        await ResetAsync();
        Guid id = await CreateAsync(key, "Existing");
        Guid other = await CreateAsync(key, "Other");
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        foreach (string name in new[] { " ", new string('X', 201), "existing", " EXISTING " })
        {
            using var rejected = await PostAsync(browser, key, "Save", await HtmlAsync(browser, key), new() { ["Name"] = name });
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            string html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
            Assert.Contains(name.Trim().Equals("existing", StringComparison.OrdinalIgnoreCase) ? "already exists" : "Enter a", html);
            using var rename = await PostAsync(browser, key, "Save", await HtmlAsync(browser, key), new()
                { ["Id"] = other.ToString(), ["Name"] = name });
            Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
            Assert.Equal("Other", (await ItemsAsync(key)).Single(item => item.Id == other).Name);
            Assert.Equal(2, (await ItemsAsync(key)).Length);
        }
        await ArchiveSetupAsync(key, id);
        using var api = app.CreateApiClient();
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonAsync(ApiPath(key), new { Name = "EXISTING" })).StatusCode);
        // Names are unique within a list, not across different lists.
        string different = key == "profiles" ? "suppliers" : "profiles";
        Assert.Equal(HttpStatusCode.Created, (await api.PostAsJsonAsync(ApiPath(different), new { Name = "Existing" })).StatusCode);
    }

    [Theory]
    [InlineData("profiles")]
    [InlineData("attribute-names")]
    [InlineData("suppliers")]
    public async Task Should_DeleteOnlyUnusedItems_When_Confirmed(string key)
    {
        await ResetAsync();
        Guid id = await CreateAsync(key, "Unused");
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string row = Row(await HtmlAsync(browser, key), id);
        Assert.Contains(">Delete</a>", row);
        Assert.DoesNotContain(">Archive</a>", row);
        string confirm = await HtmlAsync(browser, key, id);
        Assert.Contains("Cancel", confirm);
        await HtmlAsync(browser, key);
        Assert.Single(await ItemsAsync(key));
        using var deleted = await PostAsync(browser, key, "Retire", confirm, Fields(id, ReferenceAction.Delete));
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Empty(await ItemsAsync(key));
    }

    [Theory]
    [InlineData("profiles")]
    [InlineData("attribute-names")]
    [InlineData("suppliers")]
    public async Task Should_PreserveAndLabelExistingReferences_When_UsedItemIsArchived(string key)
    {
        Guid category = await ResetAsync();
        Guid id = await CreateAsync(key, "Used");
        Guid first = await ProductAsync(category);
        Guid second = await ProductAsync(category);
        await AssignAsync(key, first, id);
        await AssignAsync(key, second, id);
        if (key == "attribute-names") await AssignAsync(key, first, id, "second value");
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string row = Row(await HtmlAsync(browser, key), id);
        Assert.Contains("Used by 2 products", row);
        Assert.Contains(">Archive</a>", row);
        Assert.DoesNotContain(">Delete</a>", row);
        using var archived = await PostAsync(browser, key, "Retire", await HtmlAsync(browser, key, id), Fields(id, ReferenceAction.Archive));
        Assert.Equal(HttpStatusCode.Redirect, archived.StatusCode);
        string record = await ProductHtmlAsync(browser, first);
        Assert.Contains("Used (archived)", record);
        if (key == "attribute-names")
        {
            Assert.Contains("200 g", record);
            Assert.Contains("second value", record);
        }
        using var api = app.CreateApiClient();
        Assert.Empty((await api.GetFromJsonAsync<Choice[]>(ApiPath(key) + "/choices"))!);
        Guid fresh = await ProductAsync(category);
        await Assert.ThrowsAsync<ArgumentException>(() => AssignAsync(key, fresh, id));
        using var edited = await ProductPriceAsync(browser, first, record);
        Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        Assert.Contains("Used (archived)", await ProductHtmlAsync(browser, first));
        Assert.Equal("Used by 2 products", (await ItemsAsync(key)).Single().Usage.Description);
        if (key != "attribute-names") await AssignAsync(key, first, id); // An unchanged archived selection is preserved.
    }

    [Theory]
    [InlineData("profiles")]
    [InlineData("attribute-names")]
    [InlineData("suppliers")]
    public async Task Should_HideArchivedAndRestoreSelection_When_ToggleAndUnarchiveAreUsed(string key)
    {
        Guid category = await ResetAsync();
        Guid[] ids = new Guid[3];
        for (int index = 0; index < ids.Length; index++)
        {
            ids[index] = await CreateAsync(key, "Retired " + index);
            await ArchiveSetupAsync(key, ids[index]);
        }
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string hidden = await HtmlAsync(browser, key);
        Assert.Contains("Show archived (3)", hidden);
        Assert.DoesNotContain("Retired 0", hidden);
        string shown = await HtmlAsync(browser, key, showArchived: true);
        for (int index = 0; index < ids.Length; index++) Assert.Contains($"Retired {index} (archived)", shown);
        Assert.Contains(">Un-archive</a>", shown);
        using var restored = await PostAsync(browser, key, "Retire", await HtmlAsync(browser, key, ids[0]), Fields(ids[0], ReferenceAction.Unarchive));
        Assert.Equal(HttpStatusCode.Redirect, restored.StatusCode);
        using var api = app.CreateApiClient();
        Assert.Equal(ids[0], Assert.Single((await api.GetFromJsonAsync<Choice[]>(ApiPath(key) + "/choices"))!).Id);
        await AssignAsync(key, await ProductAsync(category), ids[0]);
        Assert.Equal("Used by 1 product", (await ItemsAsync(key)).Single(item => item.Id == ids[0]).Usage.Description);
        Assert.Contains("Show archived (2)", await HtmlAsync(browser, key));
    }

    [Fact]
    public async Task Should_PreserveValuesAndUsage_When_AttributeNameIsRenamed()
    {
        Guid category = await ResetAsync();
        Guid id = await CreateAsync("attribute-names", "Weight");
        Guid product = await ProductAsync(category);
        await AssignAsync("attribute-names", product, id);
        await AssignAsync("attribute-names", product, id, "another value");
        using var api = app.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await api.PutAsJsonAsync(ApiPath("attribute-names") + $"/{id}/name", new { Name = "Mass" })).StatusCode);
        await using var restartedApi = app.NewApi();
        await using var website = app.CreateWebsite(restartedApi);
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string record = await ProductHtmlAsync(browser, product);
        Assert.Contains("Mass", record);
        Assert.DoesNotContain(">Weight<", record);
        var details = (await api.GetFromJsonAsync<ProductDetails>($"/catalogue/products/{product}"))!;
        Assert.Equal(new[] { "200 g", "another value" }, details.Attributes.Select(value => value.Value));
        Assert.All(details.Attributes, value => Assert.Equal("Mass", value.Name));
        Assert.Equal("Used by 1 product", (await ItemsAsync("attribute-names")).Single().Usage.Description);
    }

    [Theory]
    [InlineData("profiles")]
    [InlineData("attribute-names")]
    [InlineData("suppliers")]
    public async Task Should_RefuseDelete_When_ReferenceIsAddedAfterPageLoad(string key)
    {
        Guid category = await ResetAsync();
        Guid id = await CreateAsync(key, "Previously unused");
        await using var website = app.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        string confirm = await HtmlAsync(browser, key, id);
        await AssignAsync(key, await ProductAsync(category), id);
        using var rejected = await PostAsync(browser, key, "Retire", confirm, Fields(id, ReferenceAction.Delete));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        string result = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Contains("now in use", result);
        Assert.Contains(">Archive</a>", Row(result, id));
        Assert.False((await ItemsAsync(key)).Single().IsArchived);
        using var api = app.CreateApiClient();
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonAsync(ApiPath(key) + $"/{id}/retire", new { Action = ReferenceAction.Delete })).StatusCode);
    }

    [Theory]
    [InlineData("profiles")]
    [InlineData("attribute-names")]
    [InlineData("suppliers")]
    public async Task Should_DenyMutationWithoutReplay_When_AccessIsMissing(string key)
    {
        await ResetAsync();
        Guid id = await CreateAsync(key, "Protected");
        foreach (string access in new[] { StaffRoles.FieldSalesperson, StaffRoles.SalesManager, "none", "expired" })
        {
            app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
            await using var website = app.CreateWebsite();
            using var browser = website.CreateBrowser();
            await SignInAsync(browser);
            string confirm = await HtmlAsync(browser, key, id);
            if (access == "expired")
            {
                await using var scope = website.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<StaffWebDbContext>();
                var ticket = await db.Tickets.SingleAsync();
                ticket.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
                await db.SaveChangesAsync();
            }
            else
            {
                string[] roles = access == "none" ? [] : [access];
                app.Roles.SetRoles("niamh", roles);
                website.Roles.SetRoles("niamh", roles);
            }
            using var denied = await PostAsync(browser, key, "Retire", confirm, Fields(id, ReferenceAction.Delete));
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.StartsWith(access == "expired" ? "https://localhost:7201/connect/authorize" : "/AccessChanged?state=", denied.Headers.Location!.OriginalString);
            using var api = app.CreateApiClient();
            if (access == "expired") api.DefaultRequestHeaders.Authorization = null;
            foreach (string suffix in new[] { "", $"/{id}/name", $"/{id}/retire" })
            {
                using var write = suffix.EndsWith("/name") ? await api.PutAsJsonAsync(ApiPath(key) + suffix, new { Name = "Changed" })
                    : await api.PostAsJsonAsync(ApiPath(key) + suffix, new { Name = "Added", Action = ReferenceAction.Delete });
                Assert.Equal(access == "expired" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, write.StatusCode);
            }
            app.Roles.SetRoles("niamh", StaffRoles.HeadOfficeUser);
            await SignInAsync(browser);
            await HtmlAsync(browser, key);
            Assert.Equal(id, (await ItemsAsync(key)).Single().Id);
            Assert.Equal("Protected", (await ItemsAsync(key)).Single().Name);
        }
    }

    private async Task<Guid> ResetAsync()
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        await db.ProductAttributeValues.ExecuteDeleteAsync();
        await db.ProductAlternativeBrands.ExecuteDeleteAsync();
        Guid category = await app.ResetAsync();
        await db.ProductProfiles.ExecuteDeleteAsync();
        await db.AttributeNames.ExecuteDeleteAsync();
        await db.Suppliers.ExecuteDeleteAsync();
        await db.Brands.ExecuteDeleteAsync();
        return category;
    }
    private async Task<Guid> CreateAsync(string key, string name)
    {
        using var api = app.CreateApiClient();
        using var response = await api.PostAsJsonAsync(ApiPath(key), new { Name = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Choice>())!.Id;
    }
    private async Task<ReferenceListItem[]> ItemsAsync(string key)
    {
        using var api = app.CreateApiClient();
        return (await api.GetFromJsonAsync<ReferenceListViewModel>(ApiPath(key) + "?showArchived=true"))!.Items.ToArray();
    }
    private async Task ArchiveSetupAsync(string key, Guid id)
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        NamedReferenceItem item = key switch
        {
            "profiles" => await db.ProductProfiles.SingleAsync(item => item.Id == id),
            "attribute-names" => await db.AttributeNames.SingleAsync(item => item.Id == id),
            "suppliers" => await db.Suppliers.SingleAsync(item => item.Id == id),
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
        item.Archive();
        await db.SaveChangesAsync();
    }
    private async Task<Guid> ProductAsync(Guid category)
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        var product = Product.Create(Guid.NewGuid().ToString(), "Lotion", category, 4.80m, new DateOnly(2026, 10, 1));
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }
    private async Task AssignAsync(string key, Guid productId, Guid id, string value = "200 g")
    {
        await using var scope = app.Api.Services.CreateAsyncScope();
        var assignments = scope.ServiceProvider.GetRequiredService<ProductReferenceAssignments>();
        if (key == "attribute-names") await assignments.AddAttributeAsync(productId, id, value);
        else await assignments.SetClassificationAsync(productId, key == "profiles" ? id : null, key == "suppliers" ? id : null);
    }
    private static Dictionary<string, string> Fields(Guid id, ReferenceAction action) => new()
        { ["Id"] = id.ToString(), ["Action"] = ((int)action).ToString() };
    private static string Row(string html, Guid id)
    {
        var match = Regex.Match(html, $"<section[^>]*data-reference-id=\"{id}\"[^>]*>(.*?)</section>", RegexOptions.Singleline);
        Assert.True(match.Success);
        return match.Value;
    }
    private static async Task<string> HtmlAsync(HttpClient browser, string key, Guid? confirmId = null, bool showArchived = false)
    {
        string path = Page + $"?ListKey={key}&ShowArchived={showArchived}";
        if (confirmId is Guid id) path += $"&handler=Confirm&id={id}";
        using var response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
    private static async Task<string> ProductHtmlAsync(HttpClient browser, Guid id)
    {
        using var response = await browser.GetAsync($"/HeadOffice/Products/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
    private static Task<HttpResponseMessage> ProductPriceAsync(HttpClient browser, Guid id, string html) =>
        PostToAsync(browser, $"/HeadOffice/Products/{id}?handler=Price", html,
            new() { ["BasePrice"] = "5.00", ["EffectiveFrom"] = "2026-11-01" });
    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string key, string handler, string html, Dictionary<string, string> fields)
    {
        fields["ListKey"] = key;
        fields["ShowArchived"] = "true";
        return PostToAsync(browser, Page + "?handler=" + handler, html, fields);
    }
    private static Task<HttpResponseMessage> PostToAsync(HttpClient browser, string path, string html, Dictionary<string, string> fields)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
        return browser.PostAsync(path, new FormUrlEncodedContent(fields));
    }
    private static async Task SignInAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync($"/__test/sign-in?subject=niamh&roles={StaffRoles.HeadOfficeUser}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private sealed record Choice(Guid Id, string Name, bool IsArchived);
}
