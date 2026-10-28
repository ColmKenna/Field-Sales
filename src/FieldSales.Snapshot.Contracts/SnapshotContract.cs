using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldSales.Snapshot.Contracts;

/// <summary>The Morning Snapshot contract shared by the Field Sales API and the tablet app.</summary>
/// <remarks>
/// Owner: the developer, as T-4.1.1's owner. One version change at a time, each reviewed by the owner.
/// A minor bump adds fields that older apps ignore; a major bump removes or changes a field. The server
/// keeps serving the previous major for at least one release.
/// </remarks>
public static class SnapshotContract
{
    public const string Schema = "field-sales-snapshot";
    public const int CurrentMajor = 1;
    public const int CurrentMinor = 1;
    public static string CurrentVersion => $"{CurrentMajor}.{CurrentMinor}";

    /// <summary>The majors this server can build, newest first.</summary>
    public static IReadOnlyList<int> ServedMajors { get; } = [CurrentMajor];

    public const string Path = "/tablet/snapshot";
    public const string MediaType = "application/gzip";

    /// <summary>Request header: the snapshot majors the tablet app can read, for example <c>1</c> or <c>1,2</c>.</summary>
    public const string AcceptedMajorsHeader = "X-Snapshot-Majors";
    public const string VersionHeader = "X-Snapshot-Version";
    public const string GzipSha256Header = "X-Snapshot-Sha256";
    public const string PlainSha256Header = "X-Snapshot-Plain-Sha256";

    public const string UpdateRequiredTitle = "Update the Field Sales app to sync";

    /// <summary>Web defaults with source-generated metadata, so trimmed tablet Release builds keep (de)serialization.</summary>
    public static JsonSerializerOptions JsonOptions => SnapshotJsonContext.Default.Options;

    /// <summary>Parses the tablet's accepted majors; null when the header is missing or malformed.</summary>
    public static IReadOnlyList<int>? ParseAcceptedMajors(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return null;
        List<int> majors = [];
        foreach (string part in header.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int major)
                || major < 1) return null;
            majors.Add(major);
        }
        return majors;
    }

    /// <summary>The newest major both sides support, or null when they share none.</summary>
    public static int? Negotiate(IEnumerable<int> acceptedMajors, IEnumerable<int> servedMajors)
    {
        HashSet<int> accepted = [.. acceptedMajors];
        int[] shared = servedMajors.Where(accepted.Contains).ToArray();
        return shared.Length == 0 ? null : shared.Max();
    }
}

public sealed record SnapshotManifest(string Schema, string Version, string RepSubject, DateTimeOffset BuiltUtc,
    DateOnly BuildDate, SnapshotCounts Counts);

public sealed record SnapshotCounts(int Locations, int Categories, int Products, int BasePrices,
    int ProductAttributes = 0);

/// <summary>Version 1 of the rep's Morning Snapshot.</summary>
public sealed record TabletSnapshot(SnapshotManifest Manifest, IReadOnlyList<SnapshotLocation> Locations,
    IReadOnlyList<SnapshotCategory> Categories, IReadOnlyList<SnapshotProduct> Products,
    IReadOnlyList<SnapshotBasePrice> BasePrices,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<SnapshotProductAttribute>? ProductAttributes = null);

/// <summary>An assigned Location. Locations hold no address lines yet, so its address is Town and Eircode.</summary>
public sealed record SnapshotLocation(Guid Id, string Name, Guid CustomerId, string CustomerName, SnapshotTown Town,
    string? Eircode, SnapshotContact? MainContact);

public sealed record SnapshotTown(Guid Id, string Name, string CountyName);

/// <summary>The Location's active Main Contact.</summary>
public sealed record SnapshotContact(Guid Id, string Name, string Type, string? Phone, string? Email);

/// <summary>A category on the path to an included product; the tablet derives breadcrumbs from the parents.</summary>
public sealed record SnapshotCategory(Guid Id, Guid? ParentId, string Name);

/// <summary>A product with no Restriction Group. <see cref="ParentProductId"/> is set only when the parent is included.</summary>
public sealed record SnapshotProduct(Guid Id, string Code, string Name, Guid CategoryId, string Unit,
    decimal? QuantityStep, decimal? MinimumQuantity, Guid? ParentProductId);

/// <summary>A base price in effect on the build date, or one dated after it.</summary>
public sealed record SnapshotBasePrice(Guid ProductId, DateOnly EffectiveFrom, decimal Amount);

/// <summary>A product's ordered name/value row, added in 1.1. Only included products have rows.</summary>
/// <remarks>Separate rows keep import bounded per attribute, even when a product has many attributes.
/// A 1.0 snapshot has no attribute family; names and values retain their saved text.</remarks>
public sealed record SnapshotProductAttribute(Guid ProductId, int Position, string Name, string Value, bool IsArchived);
