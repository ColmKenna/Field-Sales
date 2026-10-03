using System.Globalization;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Coverage;

public static class AssignmentHistoryFormatter
{
    public static AssignmentHistoryDetails Details(AssignmentHistory row)
    {
        HistoryOwnerDetails? before = row.PreviousRepSubject is null ? null
            : new(new(row.PreviousRepSubject, row.PreviousRepName!),
                new(row.PreviousAssignmentId!.Value, new(row.PreviousLevel!.Value, row.PreviousUnitId!.Value), row.PreviousSourceName!));
        HistoryOwnerDetails? after = row.NewRepSubject is null ? null
            : new(new(row.NewRepSubject, row.NewRepName!),
                new(row.NewAssignmentId!.Value, new(row.NewLevel!.Value, row.NewUnitId!.Value), row.NewSourceName!));
        string timestamp = row.ChangedAt.ToUniversalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
        string source = (after ?? before)!.Source.Name;
        string cause = row.Cause switch
        {
            OwnershipChangeCause.DirectLocationAssignment => "direct Location assignment",
            OwnershipChangeCause.TerritoryAssignment => "territory assignment",
            OwnershipChangeCause.GeographyChange => "geography change",
            _ => throw new InvalidOperationException("Unknown history cause.")
        };
        string display = $"{timestamp} UTC — {before?.Rep.DisplayName ?? "Unassigned"} → {after?.Rep.DisplayName ?? "Unassigned"} — via {source} — {cause} — by {row.ActorName}";
        if (row.Reason is not null) display += " — " + row.Reason;
        return new(row.Id, row.Sequence, row.OperationId, row.LocationId, row.LocationName, row.ChangedAt,
            new(row.ActorSubject, row.ActorName), before, after, row.Cause, row.Reason, display);
    }
}
