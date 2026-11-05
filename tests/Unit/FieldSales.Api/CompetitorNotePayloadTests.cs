using System.Text;
using System.Text.Json;
using FieldSales.Api.Ordering;
using FieldSales.Calls.Contracts;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Api.Tests;

/// <summary>WI-057 agreed scenario 14 (T-5.8.1 D8): the server's structure check on a Call's Competitor Notes, without the database.
/// Storing the notes is scenario 15, end to end.</summary>
[Trait("Category", "Unit")]
public sealed class CompetitorNotePayloadTests
{
    private static readonly Guid Shop = new("3c0f6a52-0000-0000-0000-000000000001");
    private static readonly CompetitorLinkedProduct Spf30 = new(new("6b2a9e11-0000-0000-0000-000000000001"), "SPF30-200", "SPF30 Sun Lotion 200ml");
    private static readonly CompetitorLinkedGroup Suncare = new(new("6b2a9e11-0000-0000-0000-0000000000c1"), "Suncare");

    private static TabletCall Call(Guid id, params CallCompetitorNote[] notes) => new(CallContract.Schema, CallContract.CurrentVersion, id,
        Shop, "colm", new DateOnly(2026, 9, 22), new DateTimeOffset(2026, 9, 22, 9, 15, 0, TimeSpan.FromHours(1)), CallChannel.InPerson,
        new("1.5", new DateTimeOffset(2026, 9, 22, 6, 30, 0, TimeSpan.Zero)), CompetitorNotes: notes);

    private static CallCompetitorNote Note(CompetitorLinkType linkTo = CompetitorLinkType.None, string text = "Competitor 250ml SPF30 on shelf",
        IReadOnlyList<CompetitorLinkedProduct>? products = null, CompetitorLinkedGroup? range = null, CompetitorLinkedGroup? category = null) =>
        new(Guid.NewGuid(), text, linkTo, products, range, category);

    [Theory]
    [InlineData("blank text", UploadFaults.Incomplete)]
    [InlineData("text too long", UploadFaults.Incomplete)]
    [InlineData("unknown link type", UploadFaults.Corrupt)]
    [InlineData("products with none", UploadFaults.Incomplete)]
    [InlineData("a product twice", UploadFaults.Incomplete)]
    [InlineData("a product with no id", UploadFaults.Incomplete)]
    [InlineData("none carrying a category", UploadFaults.Incomplete)]
    [InlineData("category carrying products", UploadFaults.Incomplete)]
    [InlineData("range with no id", UploadFaults.Incomplete)]
    [InlineData("the same note twice", UploadFaults.Incomplete)]
    public void Should_RefuseTheCall_When_ACompetitorNoteIsMalformed(string fault, string expected)
    {
        Guid id = Guid.NewGuid();
        CallCompetitorNote twice = Note();
        string body = fault switch
        {
            "blank text" => Json(Call(id, Note(text: " "))),
            "text too long" => Json(Call(id, Note(text: new string('x', CallCompetitorNote.MaximumTextLength + 1)))),
            "unknown link type" => Json(Call(id, Note())).Replace("\"linkTo\":\"None\"", "\"linkTo\":\"Supplier\"", StringComparison.Ordinal),
            "products with none" => Json(Call(id, Note(CompetitorLinkType.Products, products: []))),
            "a product twice" => Json(Call(id, Note(CompetitorLinkType.Products, products: [Spf30, Spf30]))),
            "a product with no id" => Json(Call(id, Note(CompetitorLinkType.Products, products: [Spf30 with { ProductId = Guid.Empty }]))),
            "none carrying a category" => Json(Call(id, Note(category: Suncare))),
            "category carrying products" => Json(Call(id, Note(CompetitorLinkType.Category, products: [Spf30], category: Suncare))),
            "range with no id" => Json(Call(id, Note(CompetitorLinkType.Range, range: new(Guid.Empty, "Autumn")))),
            "the same note twice" => Json(Call(id, twice, twice with { Text = "Rival stand" })),
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };

        TabletCall? read = TabletWorkReceiver.ReadCall(id, Encoding.UTF8.GetBytes(body), out ReceiveResult? refused);

        Assert.Null(read);
        Assert.Equal((ReceiveOutcome.Refused, expected), (refused!.Outcome, refused.Fault));
        // The same Call with well-formed notes, one of each link type, is accepted.
        Guid good = Guid.NewGuid();
        Assert.NotNull(TabletWorkReceiver.ReadCall(good, Encoding.UTF8.GetBytes(Json(Call(good, Note(), Note(CompetitorLinkType.Products, products: [Spf30]),
            Note(CompetitorLinkType.Category, category: Suncare), Note(CompetitorLinkType.Range, range: new(Guid.NewGuid(), "Autumn"))))), out _));
    }

    private static string Json(TabletCall call) => JsonSerializer.Serialize(call, CallJsonContext.Default.TabletCall);
}
