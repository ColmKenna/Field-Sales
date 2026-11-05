using System.Text.Json.Serialization;

namespace FieldSales.Calls.Contracts;

/// <summary>What a Competitor Note is linked to (T-5.8.1, design decision "Competitor Notes: free text with one link type"): one
/// type per note, never mixed.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompetitorLinkType>))]
public enum CompetitorLinkType { None, Products, Range, Category }

/// <summary>One of our products a note is linked to, with its code and name as captured, like a stock entry.</summary>
public sealed record CompetitorLinkedProduct(Guid ProductId, string ProductCode, string ProductName);

/// <summary>The Range or Category a note is linked to, with its name as captured: an archived Range still reads on old notes.</summary>
public sealed record CompetitorLinkedGroup(Guid Id, string Name);

/// <summary>A free-text note about competitor products seen at the Location (US-018). A Call may have several, each saved
/// separately with its own id. <paramref name="LinkTo"/> says which one of <paramref name="Products"/> (one or more),
/// <paramref name="Range"/> or <paramref name="Category"/> it carries; None carries none.</summary>
public sealed record CallCompetitorNote(Guid Id, string Text, CompetitorLinkType LinkTo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CompetitorLinkedProduct>? Products = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CompetitorLinkedGroup? Range = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CompetitorLinkedGroup? Category = null)
{
    public const int MaximumTextLength = 4000;

    public const string WriteTheNote = "Write the competitor note, or remove it";
    public static readonly string TextTooLong = $"A competitor note can be up to {MaximumTextLength:N0} characters";
    public const string TickAProduct = "Tick at least one product, or link to None";
    public const string ChooseACategory = "Choose a category, or link to None";
    public const string ChooseARange = "Choose a range, or link to None";

    /// <summary>Why this note can't be saved yet, in the rep's words, or null when it can.</summary>
    [JsonIgnore]
    public string? Problem =>
        string.IsNullOrWhiteSpace(Text) ? WriteTheNote
        : Text.Trim().Length > MaximumTextLength ? TextTooLong
        : LinkTo switch
        {
            CompetitorLinkType.Products when Products is not { Count: > 0 } => TickAProduct,
            CompetitorLinkType.Category when Category is null => ChooseACategory,
            CompetitorLinkType.Range when Range is null => ChooseARange,
            _ => null
        };

    /// <summary>Whether the note carries exactly the link its type names, each with an id and a name, products once each
    /// (D8). The tablet never builds anything else; the server refuses it.</summary>
    [JsonIgnore]
    public bool IsWellFormed => Id != Guid.Empty && Text is not null && LinkTo switch
    {
        CompetitorLinkType.None => Products is null && Range is null && Category is null,
        CompetitorLinkType.Products => Products is { Count: > 0 } products && Range is null && Category is null
            && products.All(product => product is not null && product.ProductId != Guid.Empty
                && !string.IsNullOrWhiteSpace(product.ProductCode) && !string.IsNullOrWhiteSpace(product.ProductName))
            && products.Select(product => product.ProductId).Distinct().Count() == products.Count,
        CompetitorLinkType.Range => Range is { } range && IsGroup(range) && Products is null && Category is null,
        CompetitorLinkType.Category => Category is { } category && IsGroup(category) && Products is null && Range is null,
        _ => false
    };

    /// <summary>What the note is linked to, as the rep reads it: "SPF30 200ml, SPF30 300ml", "Category: Suncare",
    /// "Range: Autumn" or "No link".</summary>
    [JsonIgnore]
    public string LinkLabel => LinkTo switch
    {
        CompetitorLinkType.Products => string.Join(", ", (Products ?? []).Select(product => product.ProductName)),
        CompetitorLinkType.Category => $"Category: {Category?.Name}",
        CompetitorLinkType.Range => $"Range: {Range?.Name}",
        _ => "No link"
    };

    /// <summary>The note as it is saved: its text trimmed.</summary>
    public CallCompetitorNote Trimmed() => this with { Text = Text.Trim() };

    /// <summary>Whether <paramref name="other"/> carries the same link as this note (Edit Call keeps links as saved, D6).</summary>
    public bool SameLinks(CallCompetitorNote other) => LinkTo == other.LinkTo && Range == other.Range && Category == other.Category
        && (Products ?? []).SequenceEqual(other.Products ?? []);

    private static bool IsGroup(CompetitorLinkedGroup group) => group.Id != Guid.Empty && !string.IsNullOrWhiteSpace(group.Name);

    /// <summary>"1 competitor note", "3 competitor notes".</summary>
    public static string Count(int notes) => notes == 1 ? "1 competitor note" : $"{notes} competitor notes";
}
