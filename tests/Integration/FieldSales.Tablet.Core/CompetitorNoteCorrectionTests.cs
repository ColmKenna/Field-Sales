using System.Text.Json;
using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-057 agreed scenario 12 (T-5.8.1 D6, completing US-015 S2): correcting a saved Call's Competitor Notes before Sync. A
/// note's text can change and a note can go; nothing is added and what a note is linked to stays as saved.</summary>
[Trait("Category", "Integration")]
public sealed class CompetitorNoteCorrectionTests : IDisposable
{
    private readonly TestTablet tablet = new();
    private readonly FakeTimeProvider at = new(new DateTimeOffset(2026, 9, 22, 9, 15, 0, TimeSpan.Zero));
    private TabletSnapshot day = null!;

    public void Dispose() => tablet.Dispose();

    private Guid Shop => day.Locations[0].Id;
    private CallStore Calls => new(tablet.Workspace(), at);
    private UnsentStore Unsent => new(tablet.Workspace());

    private CompetitorLinkedProduct Linked(int product) => new(day.Products[product].Id, day.Products[product].Code, day.Products[product].Name);

    // Murphy's, in person, recording only two notes: one linked to products 0 and 1, one to a category.
    private async Task<TabletCall> SavedAsync()
    {
        day = Snapshots.Build("colm", locations: 2, products: 5);
        await tablet.SyncAsync(day);
        CallResult saved = Calls.Save(Guid.NewGuid(), Shop, new(CallChannel.InPerson, null, null,
        [
            new(Guid.NewGuid(), "Competitor 250ml SPF30 on shelf", CompetitorLinkType.Products, [Linked(0), Linked(1)]),
            new(Guid.NewGuid(), "Rival suncare stand by the till", CompetitorLinkType.Category, Category: new(day.Categories[0].Id, day.Categories[0].Name))
        ]));
        Assert.True(saved.Succeeded, saved.Error);
        return saved.Call!;
    }

    [Theory]
    [InlineData("its text changed and the other note removed", null)]
    [InlineData("a note it didn't have", CallStore.AddsContent)]
    [InlineData("a product taken off a note's links", CallStore.NoteLinksFixed)]
    [InlineData("a note switched to None", CallStore.NoteLinksFixed)]
    [InlineData("a note's text blanked", CallCompetitorNote.WriteTheNote)]
    public async Task Should_RefuseTheCorrection_When_ItAddsANoteChangesItsLinksOrBlanksItsText(string change, string? error)
    {
        TabletCall saved = await SavedAsync();
        string before = Unsent.Find(saved.Id)!.Json;
        CallCompetitorNote products = saved.CompetitorNotes![0], category = saved.CompetitorNotes[1];
        CallCompetitorNote[] notes = change switch
        {
            "its text changed and the other note removed" => [products with { Text = "Competitor 250ml SPF30, 2 facings" }],
            "a note it didn't have" => [products, category, new(Guid.NewGuid(), "Rival tester unit", CompetitorLinkType.None)],
            "a product taken off a note's links" => [products with { Products = [products.Products![0]] }, category],
            "a note switched to None" => [products, category with { LinkTo = CompetitorLinkType.None, Category = null }],
            "a note's text blanked" => [products with { Text = " " }, category],
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };

        CallResult corrected = Calls.Correct(saved.Id, Shop, new(CallChannel.Phone, null, null, notes));

        Assert.Equal(error, corrected.Error);
        string after = Unsent.Find(saved.Id)!.Json;
        if (error is not null) { Assert.Equal(before, after); return; }
        TabletCall stored = JsonSerializer.Deserialize(after, CallJsonContext.Default.TabletCall)!;
        CallCompetitorNote kept = Assert.Single(stored.CompetitorNotes!);
        Assert.Equal((products.Id, "Competitor 250ml SPF30, 2 facings", CallChannel.Phone), (kept.Id, kept.Text, stored.Channel));
        Assert.True(kept.SameLinks(products)); // Its links as saved; a Call recording only notes could still be corrected.
    }
}
