using FieldSales.Tablet.Components.Pages;
using FieldSales.Tablet.Core;

namespace FieldSales.Tablet.Components.Tests;

/// <summary>WI-055 checkpoint: T-5.3.1's saved-call behaviour, pinned before Edit Call arrives (T-5.6.1). These must pass unchanged
/// afterwards, alongside <c>CallRecordingTests.Should_SaveOnce_When_SaveIsTappedTwice</c> (C2: one unchanged Call) and
/// <c>CallRecordingTests.Should_StayUnsentThenUploadAndMarkSent_When_TheRepSyncs</c> (C3: sent exactly as saved).</summary>
public sealed partial class TabletSessionScreenTests
{
    // C1: Sync's Unsent review of a saved Call says what it is and offers nothing that changes it.
    [Fact]
    public async Task Should_ReadCallRecordedWithItsChannel_When_ASavedCallIsReviewedFromSync()
    {
        await using Screen screen = new(typeof(Sync));
        UnsentView call = new(new(Guid.NewGuid(), UnsentKind.Call, DateTimeOffset.Parse("2026-09-22T09:15:00Z"), """{"channel":"InPerson"}"""),
            "Murphy's Pharmacy", null, "In person");
        screen.Tablet.ReadViews = _ => [call];
        screen.Tablet.ReadSummary = _ => new(1, 0, 1);
        await screen.Open(new());

        await screen.ClickLink("Murphy's Pharmacy");

        string text = screen.Renderer.Text(screen.Id);
        Assert.Contains("Call review", text);
        Assert.Contains("Call recorded · In person", text);
        Assert.DoesNotContain("Mark ready to send", screen.Renderer.Buttons(screen.Id));
        Assert.DoesNotContain("Open order", screen.Renderer.Buttons(screen.Id));
        Assert.DoesNotContain("Delete item", screen.Renderer.Buttons(screen.Id));
        Assert.Contains("Back to unsent items", screen.Renderer.Buttons(screen.Id));
    }
}
