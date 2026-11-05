using System.Text.Json;
using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-056 agreed scenarios 1, 5 and 10 (T-5.7.1, REP-US-016): a follow-up Call in storage. It is a new Call with its own
/// id, date and channel, linked to the Call it follows up, which stays as it was.</summary>
[Trait("Category", "Integration")]
public sealed class FollowUpCallTests : IDisposable
{
    private readonly TestTablet tablet = new();
    private readonly FakeTimeProvider at = new(new DateTimeOffset(2026, 9, 22, 9, 40, 0, TimeSpan.Zero));
    private TabletSnapshot day = null!;

    public void Dispose() => tablet.Dispose();

    private Guid Shop => day.Locations[0].Id;
    private CallStore Calls => new(tablet.Workspace(), at);
    private UnsentStore Unsent => new(tablet.Workspace());

    private CallStockEntry Entry(int product, decimal? count, bool low = false)
    {
        SnapshotProduct item = day.Products[product];
        return new(item.Id, item.Code, item.Name, item.Unit, count, low);
    }

    // Murphy's, in person on Tue 22 Sep at 09:40: product 0 ("Cold & Flu Relief 16s") counted 3.
    private async Task<TabletCall> OriginalAsync()
    {
        day = Snapshots.Build("colm", locations: 2, products: 5);
        await tablet.SyncAsync(day);
        CallResult saved = Calls.Save(Guid.NewGuid(), Shop, new(CallChannel.InPerson, null, [Entry(0, 3m)]));
        Assert.True(saved.Succeeded, saved.Error);
        return saved.Call!;
    }

    private static TabletCall Stored(UnsentItem item) => JsonSerializer.Deserialize(item.Json, CallJsonContext.Default.TabletCall)!;

    // Scenario 1 (S1): the next day, by phone, product 3 ("Nasal Spray 15ml") counted 2.
    [Fact]
    public async Task Should_SaveANewCallLinkedToTheOriginalWithItsOwnDateAndChannel_When_AFollowUpCountsNasalSpray15mlAs2ByPhone()
    {
        TabletCall original = await OriginalAsync();
        string before = Unsent.Find(original.Id)!.Json;
        at.Advance(TimeSpan.FromDays(1));

        Guid id = Guid.NewGuid();
        CallResult saved = Calls.Save(id, Shop, new(CallChannel.Phone, null, [Entry(3, 2m)]), followUpOf: original.Id);

        Assert.True(saved.Succeeded, saved.Error);
        TabletCall followUp = Stored(Unsent.Find(id)!);
        Assert.Equal((original.Id, Shop, CallChannel.Phone, new DateOnly(2026, 9, 23)),
            (followUp.FollowUpOf, followUp.LocationId, followUp.Channel, followUp.CalledOn));
        Assert.Equal([(day.Products[3].Id, (decimal?)2m)], followUp.StockEntries!.Select(entry => (entry.ProductId, entry.Count)));
        Assert.Equal(before, Unsent.Find(original.Id)!.Json); // The original is untouched.
        Assert.Contains("\"followUpOf\":", Unsent.Find(id)!.Json);
        Assert.DoesNotContain("followUpOf", before); // A Call that follows nothing up is written as before.
    }

    // Scenario 5 (D6): the original went at the last Sync; the follow-up is a new Unsent Call, and the sent one stays sent.
    [Fact]
    public async Task Should_SaveTheFollowUpAsANewUnsentCall_When_TheOriginalWasSent()
    {
        TabletCall original = await OriginalAsync();
        Assert.True(Unsent.MarkSent(original.Id, at.GetUtcNow()));
        at.Advance(TimeSpan.FromHours(6));

        Guid id = Guid.NewGuid();
        CallResult saved = Calls.Save(id, Shop, new(CallChannel.InPerson, null, [Entry(0, 13m)]), followUpOf: original.Id);

        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(original.Id, Stored(Assert.Single(Unsent.Read())).FollowUpOf);
        Assert.Equal((CallStanding.Unsent, CallStanding.Sent), (Calls.ReadView(id)!.Standing, Calls.ReadView(original.Id)!.Standing));
    }

    // Scenario 10: the link must name a Call this tablet holds at this shop, and not the Call itself.
    [Theory]
    [InlineData("a call this tablet doesn't hold")]
    [InlineData("a call at another shop")]
    [InlineData("itself")]
    public async Task Should_RefuseTheFollowUp_When_TheOriginalIsNotACallAtThisShopOnThisTablet(string original)
    {
        TabletCall murphys = await OriginalAsync();
        Guid elsewhere = Guid.NewGuid();
        Assert.True(Calls.Save(elsewhere, day.Locations[1].Id, new(CallChannel.InPerson, null, [Entry(0, 1m)])).Succeeded);
        Guid id = Guid.NewGuid();
        Guid link = original switch
        {
            "a call this tablet doesn't hold" => Guid.NewGuid(),
            "a call at another shop" => elsewhere,
            "itself" => id,
            _ => throw new ArgumentOutOfRangeException(nameof(original))
        };

        CallResult saved = Calls.Save(id, Shop, new(CallChannel.Phone, null, [Entry(3, 2m)]), followUpOf: link);

        Assert.Equal(CallStore.FollowUpOfMissing, saved.Error);
        Assert.Null(Unsent.Find(id));
        Assert.Equal(2, Unsent.Read().Count); // Murphy's call and the other shop's, as they were.
        Assert.NotNull(Unsent.Find(murphys.Id));
    }
}
