using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Tablet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldSales.Api.Tests;

/// <summary>WI-057 (T-5.8.1, REP-US-018): Competitor Notes from the tablet to the server. Agreed scenario 15 (end to end).</summary>
public sealed partial class SuggestedListTests
{
    [Fact]
    public async Task Should_UploadEachNoteWithItsLinks_When_ACallWithCompetitorNotesIsSynced()
    {
        (Guid murphys, Guid[] product) = await MurphysAsync(products: 2);
        Assert.True((await SyncAsync()).Completed);
        ProductSearchIndex morning = new SnapshotStore(Workspace).ReadProductIndex(new DateOnly(2026, 9, 22));
        CompetitorLinkedProduct Linked(int index)
        {
            ProductSearchResult item = morning.Find(product[index])!;
            return new(item.Id, item.Code, item.Name);
        }
        ProductSearchResult first = morning.Find(product[0])!;
        CompetitorLinkedGroup remedies = new(first.CategoryId!.Value, first.Breadcrumb);
        CallCompetitorNote[] notes =
        [
            new(Guid.NewGuid(), "Competitor 250ml SPF30 on shelf", CompetitorLinkType.Products, [Linked(0), Linked(1)]),
            new(Guid.NewGuid(), "Rival suncare stand by the till", CompetitorLinkType.Category, Category: remedies),
            new(Guid.NewGuid(), "Rival tester unit by the door", CompetitorLinkType.None)
        ];
        Guid call = Guid.NewGuid();
        CallResult saved = new CallStore(Workspace, _clock).Save(call, murphys, new(CallChannel.Phone, null, null, notes));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal("Phone · 3 competitor notes", saved.Call!.Content.Summary);

        Assert.Equal(new UploadSummary(1, 1), (await SyncAsync()).Upload);

        await using AsyncServiceScope scope = _app.Api.Services.CreateAsyncScope();
        ReceivedCall received = await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Calls.AsNoTracking()
            .Include(each => each.CompetitorNotes).ThenInclude(note => note.Products).SingleAsync(each => each.Id == call);
        Assert.Equal(
            [
                (notes[0].Id, "Competitor 250ml SPF30 on shelf", CompetitorLinkType.Products, (Guid?)null, string.Join(",", product.Order())),
                (notes[1].Id, "Rival suncare stand by the till", CompetitorLinkType.Category, remedies.Id, ""),
                (notes[2].Id, "Rival tester unit by the door", CompetitorLinkType.None, null, "")
            ],
            received.CompetitorNotes.OrderBy(note => Array.FindIndex(notes, sent => sent.Id == note.Id))
                .Select(note => (note.Id, note.Text, note.LinkTo, note.CategoryId,
                    string.Join(",", note.Products.Select(linked => linked.ProductId).Order()))).ToArray());
        Assert.All(received.CompetitorNotes, note => Assert.Null(note.RangeId));
    }
}
