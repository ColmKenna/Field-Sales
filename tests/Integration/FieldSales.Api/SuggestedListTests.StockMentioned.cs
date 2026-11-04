using FieldSales.Api.Catalogue;
using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Directory.Contracts;
using FieldSales.StaffAccess;
using FieldSales.Tablet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Api.Tests;

/// <summary>WI-050 (T-5.3.2, REP-US-006 S2 amended): a phone Call's Stock mentioned, from the tablet to the server. Agreed
/// scenario 6 end to end.</summary>
public sealed partial class SuggestedListTests
{
    [Fact]
    public async Task Should_SaveWithChannelPhoneAnd4Entries_When_FourProductsAreMentionedAtByrnes()
    {
        Geography places = await _app.SeedGeographyAsync();
        Guid byrnes = await _app.CustomerAsync("Byrne's Chemist", places.Arklow.Id);
        await _app.AssignAsync("colm", TerritoryLevel.Town, places.Arklow.Id);
        Category remedies = await _app.CategoryAsync("Remedies");
        Guid[] product = new Guid[5];
        for (int index = 0; index < product.Length; index++)
            product[index] = await _app.ProductAsync($"P-{index:D2}", $"Product {index:D2}", remedies.Id);
        Assert.True((await SyncAsync()).Completed);
        ProductSearchIndex morning = new SnapshotStore(Workspace).ReadProductIndex(new DateOnly(2026, 9, 22));
        CallStockEntry Mention(int index, decimal? count, bool low)
        {
            ProductSearchResult item = morning.Find(product[index])!;
            return new(item.Id, item.Code, item.Name, item.Unit, count, low);
        }

        // Phone, no notes: one Low, one Out, one counted, one Low with a count. Product 4 wasn't mentioned.
        CallStockEntry[] mentioned = [Mention(0, null, true), Mention(1, 0m, true), Mention(2, 3m, false), Mention(3, 2m, true)];
        Guid id = Guid.NewGuid();
        CallResult saved = new CallStore(Workspace, _clock).Save(id, byrnes, new(CallChannel.Phone, null, mentioned));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal("Phone · 3 marked Low (1 out) · 1 counted", saved.Call!.Content.Summary);

        Assert.Equal(new UploadSummary(1, 1), (await SyncAsync()).Upload);

        await using (AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope())
        {
            ReceivedCall call = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Calls.AsNoTracking()
                .Include(received => received.StockEntries).SingleAsync(received => received.Id == id);
            Assert.Equal((byrnes, CallChannel.Phone), (call.LocationId, call.Channel));
            Assert.Equal(
                [(product[0], (decimal?)null, true), (product[1], 0m, true), (product[2], 3m, false), (product[3], 2m, true)],
                call.StockEntries.OrderBy(entry => Array.IndexOf(product, entry.ProductId))
                    .Select(entry => (entry.ProductId, entry.Count, entry.Low)).ToArray());
        }
        // Stock mentioned isn't a count of the shelf: Byrne's still has no last Stock Check.
        Assert.True((await SyncAsync()).Completed);
        Assert.Empty(SuggestedFor(byrnes));
    }
}
