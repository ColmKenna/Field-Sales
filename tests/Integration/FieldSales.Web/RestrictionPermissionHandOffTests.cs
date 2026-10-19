extern alias CatalogueApi;

using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FieldSales.Directory.Contracts;
using FieldSales.ReferenceData;
using FieldSales.StaffAccess;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CatalogueDbContext = CatalogueApi::FieldSales.Api.Catalogue.CatalogueDbContext;
using CategoryTree = CatalogueApi::FieldSales.Api.Catalogue.CategoryTree;
using Product = CatalogueApi::FieldSales.Api.Catalogue.Product;
using ReferenceCatalogueRegistry = CatalogueApi::FieldSales.Api.Catalogue.ReferenceCatalogueRegistry;
using RestrictedProductVisibility = CatalogueApi::FieldSales.Api.Catalogue.RestrictedProductVisibility;
using RestrictionGroupListStore = CatalogueApi::FieldSales.Api.Catalogue.RestrictionGroupListStore;
using RestrictionPermissionReader = CatalogueApi::FieldSales.Api.Coverage.RestrictionPermissionReader;
using RestrictionPermissionUsageSource = CatalogueApi::FieldSales.Api.Coverage.RestrictionPermissionUsageSource;
using static FieldSales.Web.Tests.RestrictionPermissionHost;

namespace FieldSales.Web.Tests;

// WI-029 increment 4: permissions counted as Restriction Group usage (archive messages and delete
// protection), and the current-permissions hand-off to the snapshot builder (WI-043).
public sealed class RestrictionPermissionHandOffTests(CoverageReadApplication app)
    : IClassFixture<CoverageReadApplication>, IAsyncLifetime
{
    private const RestrictionPermissionChangeKind Granted = RestrictionPermissionChangeKind.Granted;
    private const RestrictionPermissionChangeKind Removed = RestrictionPermissionChangeKind.Removed;
    private const string ReferencePage = "/HeadOffice/ReferenceData";
    private const string Pharmacy = "Pharmacy-only medicines";
    private const string HighValue = "High-value equipment";
    private const string Training = "Completed training 15 Sep 2026";
    private RestrictionPermissionHost _host = null!;

    public Task InitializeAsync()
    {
        _host = new(app);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Should_CountCurrentGrantsInArchiveAndUnarchiveNotes_When_RepsHoldTheGroupsPermission()
    {
        var group = (await _host.ResetAsync(HighValue))[0];
        using (var headOffice = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser))
        {
            foreach (string rep in new[] { "colm", "aoife", "ciara" })
                await SavedAsync(ChangeAsync(headOffice, rep, group, Granted, Training));
            // Brian's permission was removed, so it is not counted.
            var brian = await SavedAsync(ChangeAsync(headOffice, "brian", group, Granted, Training));
            await SavedAsync(ChangeAsync(headOffice, "brian", group, Removed, "Left the team", brian.Id));
        }
        await using var website = _host.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);

        string row = Row(await ReferenceHtmlAsync(browser), group);
        Assert.Contains("Used by 3 permissions", row);
        Assert.Contains(">Archive</a>", row);
        Assert.DoesNotContain(">Delete</a>", row);
        string archive = await ReferenceHtmlAsync(browser, group);
        Assert.Contains("<p>3 permissions will be kept but have no effect while archived</p>", archive);
        using (var archived = await RetireAsync(browser, archive, group, ReferenceAction.Archive))
            Assert.Equal(HttpStatusCode.Redirect, archived.StatusCode);
        string restore = await ReferenceHtmlAsync(browser, group, showArchived: true);
        Assert.Contains("<p>3 permissions will take effect again</p>", restore);
        using (var restored = await RetireAsync(browser, restore, group, ReferenceAction.Unarchive))
            Assert.Equal(HttpStatusCode.Redirect, restored.StatusCode);
        Assert.Equal((5, 3), await _host.CountsAsync()); // archiving kept every permission
    }

    [Fact]
    public async Task Should_OfferArchiveOrDelete_When_TheGroupIsHeldOrOnlyHasRemovedPermissions()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        await using var website = _host.CreateWebsite();
        using var browser = website.CreateBrowser();
        await SignInAsync(browser);
        using var headOffice = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);

        // A delete confirmed before Colm was granted the group is refused once he holds it.
        string staleDelete = await ReferenceHtmlAsync(browser, group);
        Assert.Contains(">Delete</a>", Row(await ReferenceHtmlAsync(browser), group));
        var granted = await SavedAsync(ChangeAsync(headOffice, "colm", group, Granted, Training));
        using (var refused = await RetireAsync(browser, staleDelete, group, ReferenceAction.Delete))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            string result = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());
            Assert.Contains("now in use", result);
            Assert.Contains("Used by 1 permission", Row(result, group));
            Assert.Contains(">Archive</a>", Row(result, group));
            Assert.DoesNotContain(">Delete</a>", Row(result, group));
        }

        // Once the permission is removed only the record refers to the group, so it can be deleted.
        await SavedAsync(ChangeAsync(headOffice, "colm", group, Removed, null, granted.Id));
        string row = Row(await ReferenceHtmlAsync(browser), group);
        Assert.Contains("Not used yet", row);
        Assert.Contains(">Delete</a>", row);
        using (var deleted = await RetireAsync(browser, await ReferenceHtmlAsync(browser, group), group, ReferenceAction.Delete))
            Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);

        await using var scope = _host.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        Assert.False(await db.RestrictionGroups.AnyAsync());
        Assert.Equal([Pharmacy, Pharmacy], await db.RestrictionPermissionChanges.OrderBy(row => row.Sequence)
            .Select(row => row.RestrictionGroupName).ToArrayAsync());
    }

    [Fact]
    public async Task Should_ReturnColmsHeldGroups_When_TheSnapshotBuilderAsksForCurrentPermissions()
    {
        var groups = await _host.ResetAsync(Pharmacy, HighValue);
        Guid pharmacyProduct = await ProductAsync("Hay fever tablets", groups[0]);
        Guid highValueProduct = await ProductAsync("Blood pressure monitor", groups[1]);
        Guid everyday = await ProductAsync("Sun lotion", null);
        using var headOffice = _host.ApiAs("orla", BusinessRoles.HeadOfficeUser);
        var granted = await SavedAsync(ChangeAsync(headOffice, "colm", groups[0], Granted, Training));

        Assert.Equal([groups[0]], await CurrentAsync("colm"));
        Assert.Empty(await CurrentAsync("aoife"));
        Assert.Equal(new[] { pharmacyProduct, everyday }.Order(), await VisibleAsync("colm"));
        Assert.Equal([everyday], await VisibleAsync("aoife"));
        Assert.DoesNotContain(highValueProduct, await VisibleAsync("colm"));

        // While the group is archived Colm still holds the permission, but its products are hidden (MI-45).
        await _host.ArchiveAsync(groups[0]);
        Assert.Equal([groups[0]], await CurrentAsync("colm"));
        Assert.Equal([everyday], await VisibleAsync("colm"));
        await _host.ArchiveAsync(groups[0], archived: false);
        Assert.Equal(new[] { pharmacyProduct, everyday }.Order(), await VisibleAsync("colm"));

        // Once removed, his next snapshot leaves the group's products out.
        await SavedAsync(ChangeAsync(headOffice, "colm", groups[0], Removed, null, granted.Id));
        Assert.Empty(await CurrentAsync("colm"));
        Assert.Equal([everyday], await VisibleAsync("colm"));
    }

    [Fact]
    public async Task Should_FailClosed_When_ThePermissionSourceIsNotRegistered()
    {
        var group = (await _host.ResetAsync(Pharmacy))[0];
        await using (var scope = _host.Api.Services.CreateAsyncScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<ReferenceCatalogueRegistry>();
            registry.ValidateRegistrations();
            Assert.Equal(["products", RestrictionGroupListStore.PermissionSource],
                registry.Find(ReferenceListKeys.RestrictionGroups)!.Definition.RequiredUsageSources);
            Assert.Contains(registry.SourcesFor(ReferenceListKeys.RestrictionGroups), source => source is RestrictionPermissionUsageSource);
        }

        // Without the source, Restriction Groups can be neither listed nor retired: a missing count is not zero.
        await using var missing = _host.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            foreach (var real in services.Where(service => service.ServiceType == typeof(IReferenceUsageSource)
                && service.ImplementationType == typeof(RestrictionPermissionUsageSource)).ToArray())
                services.Remove(real);
        }));
        using var api = missing.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        api.DefaultRequestHeaders.Authorization = new("Bearer", app.Token());
        using (var list = await api.GetAsync("/catalogue/reference-data/" + ReferenceListKeys.RestrictionGroups))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, list.StatusCode);
        using (var retire = await api.PostAsJsonAsync($"/catalogue/reference-data/{ReferenceListKeys.RestrictionGroups}/{group}/retire",
            new { Action = ReferenceAction.Delete }))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, retire.StatusCode);
        await using var check = _host.Api.Services.CreateAsyncScope();
        Assert.True(await check.ServiceProvider.GetRequiredService<CatalogueDbContext>().RestrictionGroups.AnyAsync(row => row.Id == group));
    }

    private async Task SignInAsync(HttpClient browser)
    {
        _host.SetRoles("niamh", BusinessRoles.HeadOfficeUser);
        using var response = await browser.GetAsync("/__test/sign-in?subject=niamh&roles=" + Uri.EscapeDataString(BusinessRoles.HeadOfficeUser));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string> ReferenceHtmlAsync(HttpClient browser, Guid? confirm = null, bool showArchived = false)
    {
        string path = ReferencePage + $"?ListKey={ReferenceListKeys.RestrictionGroups}&ShowArchived={showArchived}";
        if (confirm is Guid id) path += $"&handler=Confirm&id={id}";
        using var response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> RetireAsync(HttpClient browser, string html, Guid id, ReferenceAction action)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        var fields = new Dictionary<string, string>
        {
            ["Id"] = id.ToString(), ["Action"] = ((int)action).ToString(), ["ListKey"] = ReferenceListKeys.RestrictionGroups,
            ["ShowArchived"] = "true", ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        };
        return browser.PostAsync(ReferencePage + "?handler=Retire", new FormUrlEncodedContent(fields));
    }

    private static string Row(string html, Guid id)
    {
        var match = Regex.Match(html, $"<section[^>]*data-reference-id=\"{id}\"[^>]*>(.*?)</section>", RegexOptions.Singleline);
        Assert.True(match.Success);
        return match.Value;
    }

    private async Task<Guid> ProductAsync(string name, Guid? group)
    {
        await using var scope = _host.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        var category = new CategoryTree([]).Add(Guid.NewGuid().ToString("N"));
        var product = Product.Create(Guid.NewGuid().ToString("N"), name, category.Id, 4.80m, new DateOnly(2026, 10, 1));
        product.SetRestrictionGroup(group is Guid id ? await db.RestrictionGroups.SingleAsync(row => row.Id == id) : null);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<Guid[]> CurrentAsync(string rep)
    {
        await using var scope = _host.Api.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<RestrictionPermissionReader>().CurrentGroupIdsAsync(rep, default)).ToArray();
    }

    // What the snapshot builder will do: the rep's current permissions through the one visibility rule.
    private async Task<Guid[]> VisibleAsync(string rep)
    {
        await using var scope = _host.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        var permitted = await scope.ServiceProvider.GetRequiredService<RestrictionPermissionReader>().CurrentGroupIdsAsync(rep, default);
        // Sorted here: SQL Server orders uniqueidentifier values differently from .NET.
        return (await RestrictedProductVisibility.VisibleToRep(db.Products, db.RestrictionGroups, permitted)
            .Select(product => product.Id).ToArrayAsync()).Order().ToArray();
    }
}
