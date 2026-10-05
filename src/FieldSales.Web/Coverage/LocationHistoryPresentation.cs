using System.Globalization;
using FieldSales.Directory.Contracts;

namespace FieldSales.Web.Coverage;

public static class LocationHistoryPresentation
{
    public static string Change(AssignmentHistoryDetails row)
    {
        var source = (row.NewOwner ?? row.PreviousOwner)!.Source;
        string cause = row.Cause switch
        {
            OwnershipChangeCause.GeographyChange => $"geography change — via {source.Name}",
            OwnershipChangeCause.DirectLocationAssignment => "direct Location assignment",
            _ => $"via {source.Name} assignment"
        };
        return $"{row.PreviousOwner?.Rep.DisplayName ?? "Unassigned"} → {row.NewOwner?.Rep.DisplayName ?? "Unassigned"} — {cause} — by {row.Actor.DisplayName}"
            + (string.IsNullOrWhiteSpace(row.Reason) ? "" : " — " + row.Reason);
    }

    public static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
}
