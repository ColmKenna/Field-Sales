using FieldSales.Calls.Contracts;

namespace FieldSales.Tablet.Core.Tests;

/// <summary>WI-057 agreed scenarios 8 and 9 (T-5.8.1, REP-US-018 D2): a Competitor Note counts as something recorded, and a note that
/// can't be saved says why in the rep's words.</summary>
[Trait("Category", "Unit")]
public sealed class CompetitorNoteContentTests
{
    private static readonly CompetitorLinkedGroup Suncare = new(new("00000005-0000-0000-0000-000000000031"), "Suncare");

    private static CallCompetitorNote Note(string text, CompetitorLinkType linkTo = CompetitorLinkType.None,
        IReadOnlyList<CompetitorLinkedProduct>? products = null, CompetitorLinkedGroup? range = null, CompetitorLinkedGroup? category = null) =>
        new(Guid.NewGuid(), text, linkTo, products, range, category);

    // Scenario 8 (D2): In person, nothing else recorded.
    [Fact]
    public void Should_SaveACallWithOnlyACompetitorNote_When_NothingElseIsRecorded()
    {
        CallContent content = new(CallChannel.InPerson, null, null, [Note("  Rival suncare stand by the till  ", CompetitorLinkType.Category, category: Suncare)]);

        Assert.Null(content.Problem);
        Assert.Equal("In person · 1 competitor note", content.Summary);
        Assert.Equal("Rival suncare stand by the till", Assert.Single(content.SavedNotes!).Text); // Saved trimmed.
        Assert.Equal(CallContract.RecordSomething, new CallContent(CallChannel.InPerson, null).Problem); // And without it, nothing.
    }

    // Scenario 9 (D2).
    [Theory]
    [InlineData("no text", CallCompetitorNote.WriteTheNote)]
    [InlineData("only spaces", CallCompetitorNote.WriteTheNote)]
    [InlineData("too long", "A competitor note can be up to 4,000 characters")]
    [InlineData("products with none ticked", CallCompetitorNote.TickAProduct)]
    [InlineData("category with none chosen", CallCompetitorNote.ChooseACategory)]
    [InlineData("range with none chosen", CallCompetitorNote.ChooseARange)]
    public void Should_RefuseTheNote_When_ItHasNoTextIsTooLongOrItsLinkTypeHasNoPick(string note, string problem)
    {
        CallCompetitorNote refused = note switch
        {
            "no text" => Note(""),
            "only spaces" => Note("   ", CompetitorLinkType.Category, category: Suncare),
            "too long" => Note(new string('x', CallCompetitorNote.MaximumTextLength + 1)),
            "products with none ticked" => Note("Competitor 250ml SPF30 on shelf", CompetitorLinkType.Products, products: []),
            "category with none chosen" => Note("Rival suncare stand by the till", CompetitorLinkType.Category),
            "range with none chosen" => Note("Rival autumn range", CompetitorLinkType.Range),
            _ => throw new ArgumentOutOfRangeException(nameof(note))
        };
        // Pitch notes are recorded too, so the note's own problem is the one that stops the save.
        CallContent content = new(CallChannel.InPerson, "Presented autumn range", null, [Note("Fine note"), refused]);

        Assert.Equal(problem, content.Problem);
        Assert.Null(new CallContent(CallChannel.InPerson, null, null, [Note(new string('x', CallCompetitorNote.MaximumTextLength))]).Problem);
    }
}
