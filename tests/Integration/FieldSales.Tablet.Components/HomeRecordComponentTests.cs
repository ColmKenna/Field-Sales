using System.Net;
using FieldSales.Tablet.Components.Shared;
using FieldSales.Tablet.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FieldSales.Tablet.Components.Tests;

public sealed class HomeRecordComponentTests
{
    private static readonly HomeLocation Murphys = new(Guid.Parse("48000000-0000-0000-0000-000000000001"), "Murphy's Pharmacy", "Rathdrum", "Murphy's");
    private static readonly HomeLocation Nolans = new(Guid.Parse("48000000-0000-0000-0000-000000000002"), "Nolan's Pharmacy", "Rathdrum", "Nolan's");
    private static readonly DateTimeOffset At = new(2026, 9, 22, 9, 40, 0, TimeSpan.Zero);

    [Fact]
    public async Task Should_RenderTheS2RecordWithNamedStatesAndShopLinks_When_TheDayHasBeenCaptured()
    {
        string html = await Render(S2());
        Assert.Contains("Murphy's Pharmacy", html);
        Assert.Contains("Done", html);
        Assert.Contains("Call 09:40", html);
        Assert.Contains("Ready to Send", html);
        Assert.Contains("Nolan's Pharmacy", html);
        Assert.Contains("Unplanned", html);
        Assert.Contains("Saved 11:15", html);
        Assert.Contains($"/locations/{Murphys.Id}", html);
        Assert.Contains($"/locations/{Nolans.Id}", html);
        Assert.Contains("Unsent: 3", html);
        Assert.True(html.IndexOf("Murphy's", StringComparison.Ordinal) < html.IndexOf("Nolan's", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_KeepTheEmptyPlanAndUnplannedActivity_When_ProductionHasNoVisitSource()
    {
        HomeDayRecord s2 = S2();
        var production = s2 with { Sections = [s2.Sections[0] with { Locations = [] },
            s2.Sections[1] with { Locations = [s2.Sections[0].Locations[0] with { IsPlanned = false }, .. s2.Sections[1].Locations] }] };
        string html = await Render(production);
        Assert.Contains("No visits scheduled today", html);
        Assert.Contains("Murphy's Pharmacy", html);
        Assert.Contains("Nolan's Pharmacy", html);
    }

    [Fact]
    public async Task Should_AppendACounterAndSectionWithoutChangingExistingContent_When_ExtensionsAreSupplied()
    {
        HomeDayRecord original = S2();
        var extended = original with
        {
            Counters = [.. original.Counters, new("fixture", "Fixture counter", 2, "/fixture")],
            Sections = [.. original.Sections, new("fixture", "Fixture section", [], "Additional section content")]
        };
        string html = await Render(extended);
        Assert.Contains("Fixture counter: 2", html);
        Assert.Contains("Fixture section", html);
        Assert.Contains("Additional section content", html);
        Assert.Contains("Unsent: 3", html);
        Assert.Contains("Murphy's Pharmacy", html);
        Assert.Contains("Saved 11:15", html);
        Assert.True(html.IndexOf("Unplanned today", StringComparison.Ordinal) < html.IndexOf("Fixture section", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_ShowZeroAsTextInsteadOfAnActiveException_When_ThereIsNothingUnsent()
    {
        var empty = S2() with { Counters = [new("unsent", "Unsent", 0, "/sync")], Sections = [] };
        string html = await Render(empty);
        Assert.Contains("Unsent: 0", html);
        Assert.DoesNotContain("href=\"/sync\"", html);
    }

    private static HomeDayRecord S2() => new(new(2026, 9, 22), [new("unsent", "Unsent", 3, "/sync")],
        [new("today", "Today", [new(Murphys, true, [new(Guid.NewGuid(), At, "In person")],
            [new(Guid.NewGuid(), At, OrderStanding.ReadyToSend, 12)])], "No visits scheduled today"),
         new("unplanned", "Unplanned today", [new(Nolans, false, [],
            [new(Guid.NewGuid(), At.AddMinutes(95), OrderStanding.ReadyToSend, 3)])])]);

    private static async Task<string> Render(HomeDayRecord record)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using HtmlRenderer renderer = new(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<HomeDayView>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(HomeDayView.Record)] = record }));
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }
}
