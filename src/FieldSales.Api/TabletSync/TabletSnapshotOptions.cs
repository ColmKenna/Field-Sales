using System.ComponentModel.DataAnnotations;

namespace FieldSales.Api.TabletSync;

/// <summary>Settings for the rep's Morning Snapshot, from the <c>TabletSnapshot</c> configuration section.</summary>
/// <remarks>Configuration only, until MI-12 decides where settings are kept and who changes them (WI-049 D2).</remarks>
public sealed class TabletSnapshotOptions
{
    public const string Section = "TabletSnapshot";

    /// <summary>How many of a Location's most recent Accepted Orders feed its Suggested List (T-5.4.1). Default 3.</summary>
    [Range(1, 10)]
    public int SuggestedListOrderCount { get; set; } = 3;
}
