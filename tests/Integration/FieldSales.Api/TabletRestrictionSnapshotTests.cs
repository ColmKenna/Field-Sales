using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Snapshot.Contracts;
using FieldSales.StaffAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Api.Tests;

// WI-043 increment 2: approved A2-A8. Exercise the real authenticated, serialized snapshot boundary.
public sealed partial class TabletSnapshotApiTests
{
    [Fact]
    public async Task Should_ExcludeHiddenProductFacts_When_ASnapshotIsSerialized()
    {
        Category publicCategory = await _app.CategoryAsync("Public shelf");
        Category privateCategory = await _app.CategoryAsync("Denied-only category");
        RestrictionGroup held = await _app.RestrictionGroupAsync("Held group");
        RestrictionGroup denied = await _app.RestrictionGroupAsync("Denied group");
        Guid unrestricted = await _app.ProductAsync("PUBLIC-1", "Ordinary product", publicCategory.Id);
        Guid permitted = await _app.ProductAsync("PERMITTED-1", "Permitted product", publicCategory.Id, group: held);
        Guid hidden = await _app.ProductAsync("DENIED-SECRET-1", "Denied secret product", privateCategory.Id, group: denied,
            price: 987.65m);
        await _app.AddPriceAsync(hidden, 1234.56m, new(2026, 12, 1));
        await _app.AddPriceAsync(permitted, 6.75m, new(2026, 12, 1));
        await AddSnapshotFactAsync(unrestricted, "PUBLIC FACT");
        await AddSnapshotFactAsync(permitted, "PERMITTED FACT");
        await AddSnapshotFactAsync(hidden, "DENIED SECRET FACT");
        await _app.GrantAsync("colm", held.Id);
        await _app.GrantAsync("aoife", denied.Id);
        using HttpClient client = _app.ClientAs("colm", [BusinessRoles.FieldSalesperson]);
        using HttpResponseMessage response = await TabletSnapshotApplication.RequestAsync(client, SnapshotContract.Path, "1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using Stream compressed = await response.Content.ReadAsStreamAsync();
        using GZipStream gzip = new(compressed, CompressionMode.Decompress);
        using StreamReader reader = new(gzip, Encoding.UTF8);
        string payload = await reader.ReadToEndAsync();
        TabletSnapshot snapshot = JsonSerializer.Deserialize<TabletSnapshot>(payload, SnapshotContract.JsonOptions)!;

        foreach (string secret in new[] { hidden.ToString(), "DENIED-SECRET-1", "Denied secret product", "DENIED SECRET FACT",
                     privateCategory.Id.ToString(), "Denied-only category", "987.65", "1234.56" })
            Assert.DoesNotContain(secret, payload);
        Assert.Equal(new[] { unrestricted, permitted }.Order(), snapshot.Products.Select(product => product.Id).Order());
        Assert.Equal(["PERMITTED FACT", "PUBLIC FACT"], snapshot.ProductAttributes!.Select(fact => fact.Value).Order());
        Assert.Contains(new SnapshotBasePrice(permitted, new(2026, 12, 1), 6.75m), snapshot.BasePrices);
        Assert.Equal(new SnapshotCounts(0, 1, 2, 3, 2), snapshot.Manifest.Counts);
    }

    [Fact]
    public async Task Should_KeepReferencesWithinVisibleProducts_When_AHiddenProductIsAParent()
    {
        Category root = await _app.CategoryAsync("Public root");
        Category publicShelf = await _app.CategoryAsync("Public shelf", root.Id);
        Category secretShelf = await _app.CategoryAsync("Secret shelf", root.Id);
        RestrictionGroup denied = await _app.RestrictionGroupAsync("Denied group");
        Guid hidden = await _app.ProductAsync("SECRET-PARENT", "Hidden parent", secretShelf.Id, group: denied);
        Guid child = await _app.ProductAsync("CHILD-1", "Visible child", publicShelf.Id);
        Guid parent = await _app.ProductAsync("PARENT-1", "Visible parent", publicShelf.Id);
        Guid secondChild = await _app.ProductAsync("CHILD-2", "Second visible child", publicShelf.Id);
        await _app.SetParentAsync(child, hidden);
        await _app.SetParentAsync(secondChild, parent);

        TabletSnapshot snapshot = await _app.SyncAsync("colm");

        Assert.Null(snapshot.Products.Single(product => product.Id == child).ParentProductId);
        Assert.Equal(parent, snapshot.Products.Single(product => product.Id == secondChild).ParentProductId);
        Assert.Equal(new[] { child, parent, secondChild }.Order(), snapshot.Products.Select(product => product.Id).Order());
        Assert.Equal(new[] { root.Id, publicShelf.Id }.Order(), snapshot.Categories.Select(category => category.Id).Order());
        Assert.Equal(["Public root", "Public shelf"], Breadcrumb(snapshot.Categories.ToDictionary(category => category.Id), publicShelf.Id));
    }

    [Fact]
    public async Task Should_IncludeNewlyPermittedProducts_When_TheRepSyncsAfterAGrant()
    {
        Category category = await _app.CategoryAsync("Shelf");
        RestrictionGroup group = await _app.RestrictionGroupAsync("Pharmacy-only medicines");
        Guid publicProduct = await _app.ProductAsync("PUBLIC", "Ordinary product", category.Id);
        Guid restricted = await _app.ProductAsync("CONTROLLED", "Controlled Pain Relief 30s", category.Id, group: group);
        await AddSnapshotFactAsync(restricted, "Granted product fact");
        using HttpClient sameSignIn = _app.ClientAs("colm", [BusinessRoles.FieldSalesperson]);
        Assert.Equal([publicProduct], (await SnapshotForAsync(sameSignIn)).Products.Select(product => product.Id));

        await _app.GrantAsync("colm", group.Id);
        TabletSnapshot next = await SnapshotForAsync(sameSignIn);

        Assert.Equal(new[] { publicProduct, restricted }.Order(), next.Products.Select(product => product.Id).Order());
        Assert.Contains(next.BasePrices, price => price.ProductId == restricted);
        Assert.Equal("Granted product fact", Assert.Single(next.ProductAttributes!).Value);
    }

    [Fact]
    public async Task Should_OmitNewlyDeniedProducts_When_TheRepSyncsAfterRemoval()
    {
        Category category = await _app.CategoryAsync("Shelf");
        RestrictionGroup first = await _app.RestrictionGroupAsync("First group");
        RestrictionGroup second = await _app.RestrictionGroupAsync("Second group");
        Guid unrestricted = await _app.ProductAsync("PUBLIC", "Ordinary product", category.Id);
        Guid removed = await _app.ProductAsync("REMOVED", "Formerly permitted", category.Id, group: first);
        Guid retained = await _app.ProductAsync("RETAINED", "Still permitted", category.Id, group: second);
        await AddSnapshotFactAsync(removed, "Removed fact");
        await AddSnapshotFactAsync(retained, "Retained fact");
        await _app.AddPriceAsync(removed, 9.99m, new(2026, 12, 1));
        await _app.GrantAsync("colm", first.Id);
        await _app.GrantAsync("colm", second.Id);
        using HttpClient sameSignIn = _app.ClientAs("colm", [BusinessRoles.FieldSalesperson]);
        Assert.Equal(3, (await SnapshotForAsync(sameSignIn)).Products.Count);

        await using (AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().RestrictionPermissions
                .Where(grant => grant.RepSubject == "colm" && grant.RestrictionGroupId == first.Id).ExecuteDeleteAsync());
        TabletSnapshot next = await SnapshotForAsync(sameSignIn);

        Assert.Equal(new[] { unrestricted, retained }.Order(), next.Products.Select(product => product.Id).Order());
        Assert.Equal(new[] { unrestricted, retained }.Order(), next.BasePrices.Select(price => price.ProductId).Distinct().Order());
        Assert.Equal(retained, Assert.Single(next.ProductAttributes!).ProductId);
        Assert.Equal("Retained fact", next.ProductAttributes![0].Value);
    }

    [Fact]
    public async Task Should_OmitAProduct_When_ItMovesIntoAnUnpermittedGroup()
    {
        Category category = await _app.CategoryAsync("Shelf");
        RestrictionGroup group = await _app.RestrictionGroupAsync("Pharmacy-only medicines");
        Guid moving = await _app.ProductAsync("MOVING", "Newly controlled product", category.Id);
        Guid publicProduct = await _app.ProductAsync("PUBLIC", "Ordinary product", category.Id);
        await AddSnapshotFactAsync(moving, "Previously public fact");
        Assert.Equal(2, (await _app.SyncAsync("colm")).Products.Count);

        await _app.RestrictAsync(moving, group.Id);
        TabletSnapshot next = await _app.SyncAsync("colm");

        Assert.Equal([publicProduct], next.Products.Select(product => product.Id));
        Assert.Equal(publicProduct, Assert.Single(next.BasePrices).ProductId);
        Assert.Empty(next.ProductAttributes!);
    }

    [Fact]
    public async Task Should_HideAnArchivedGroupsProducts_When_TheRepStillHoldsPermission()
    {
        Category category = await _app.CategoryAsync("Shelf");
        RestrictionGroup group = await _app.RestrictionGroupAsync("Pharmacy-only medicines");
        Guid restricted = await _app.ProductAsync("CONTROLLED", "Controlled Pain Relief 30s", category.Id, group: group);
        Guid publicProduct = await _app.ProductAsync("PUBLIC", "Ordinary product", category.Id);
        await AddSnapshotFactAsync(restricted, "Restricted fact");
        await _app.GrantAsync("colm", group.Id);
        Assert.Equal(2, (await _app.SyncAsync("colm")).Products.Count);

        await _app.ArchiveGroupAsync(group.Id);

        foreach (string rep in new[] { "colm", "aoife" })
        {
            TabletSnapshot next = await _app.SyncAsync(rep);
            Assert.Equal([publicProduct], next.Products.Select(product => product.Id));
            Assert.Equal(publicProduct, Assert.Single(next.BasePrices).ProductId);
            Assert.Empty(next.ProductAttributes!);
        }
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        var grants = await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().RestrictionPermissions.ToArrayAsync();
        var kept = Assert.Single(grants);
        Assert.Equal(("colm", group.Id), (kept.RepSubject, kept.RestrictionGroupId));
    }

    [Fact]
    public async Task Should_RestorePriorVisibility_When_ARestrictionGroupIsUnarchived()
    {
        Category category = await _app.CategoryAsync("Shelf");
        RestrictionGroup group = await _app.RestrictionGroupAsync("Pharmacy-only medicines");
        Guid restricted = await _app.ProductAsync("CONTROLLED", "Controlled Pain Relief 30s", category.Id, group: group);
        Guid publicProduct = await _app.ProductAsync("PUBLIC", "Ordinary product", category.Id);
        await AddSnapshotFactAsync(restricted, "Restored fact");
        await _app.GrantAsync("colm", group.Id);
        TabletSnapshot before = await _app.SyncAsync("colm");
        await _app.ArchiveGroupAsync(group.Id);
        Assert.Equal([publicProduct], (await _app.SyncAsync("colm")).Products.Select(product => product.Id));

        await using (AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope())
        {
            CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            (await db.RestrictionGroups.SingleAsync(row => row.Id == group.Id)).Unarchive();
            await db.SaveChangesAsync();
        }
        TabletSnapshot holder = await _app.SyncAsync("colm"), nonHolder = await _app.SyncAsync("aoife");

        Assert.Equal(before.Products, holder.Products);
        Assert.Equal(before.BasePrices, holder.BasePrices);
        Assert.Equal(before.ProductAttributes, holder.ProductAttributes);
        Assert.Equal([publicProduct], nonHolder.Products.Select(product => product.Id));
        Assert.Equal(publicProduct, Assert.Single(nonHolder.BasePrices).ProductId);
        Assert.Empty(nonHolder.ProductAttributes!);
        await using AsyncServiceScope check = _app.Api.Services.CreateAsyncScope();
        var kept = Assert.Single(await check.ServiceProvider.GetRequiredService<CatalogueDbContext>().RestrictionPermissions.ToArrayAsync());
        Assert.Equal(("colm", group.Id), (kept.RepSubject, kept.RestrictionGroupId));
    }

    private async Task AddSnapshotFactAsync(Guid productId, string value)
    {
        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        CatalogueDbContext db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
        AttributeName name = AttributeName.Create($"Handling {productId:N}");
        db.AttributeNames.Add(name);
        (await db.Products.SingleAsync(product => product.Id == productId)).AddAttribute(name, value);
        await db.SaveChangesAsync();
    }

    private static async Task<TabletSnapshot> SnapshotForAsync(HttpClient client)
    {
        using HttpResponseMessage response = await TabletSnapshotApplication.RequestAsync(client, SnapshotContract.Path, "1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TabletSnapshotApplication.ReadAsync(response);
    }
}
