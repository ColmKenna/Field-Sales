using System.Security.Cryptography;
using System.Text;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Coverage;

public sealed record TransferUnit(TerritoryTarget Target, TerritoryTarget? Parent, string Name, bool Archived);
public sealed record AssignmentTransferPlan(IReadOnlyList<TerritoryAssignment> Assignments, IReadOnlyList<string> Notices);

// Complete geography (including empty/archived units), not Location counts, determines roll-ups.
// Existing narrower assignments are independent scopes and are never implicitly moved.
public static class AssignmentTransferPlanner
{
    public static AssignmentTransferPlan Calculate(IReadOnlyList<TransferUnit> geography,
        IReadOnlyList<TerritoryAssignment> assignments, string source, string recipient,
        IReadOnlyList<TransferSelection> selections, string recipientName)
    {
        CoverageSubjects.Validate(source, "SourceRepSubject"); CoverageSubjects.Validate(recipient, "ReceivingRepSubject");
        if (source == recipient) throw Invalid("Choose a different receiving rep.");
        var units = geography.ToDictionary(row => row.Target);
        var byId = assignments.ToDictionary(row => row.Id);
        var byTarget = assignments.ToDictionary(row => row.Target);
        var selected = new HashSet<TransferSelection>();
        foreach (var selection in selections)
        {
            if (selection is null || selection.Target is null || !byId.TryGetValue(selection.AssignmentId, out var parent)
                || parent.RepSubject != source || !units.TryGetValue(selection.Target, out var unit))
                throw Invalid("Reload the giving rep's assignments and choose their territory.");
            if (selection.Target != parent.Target && (unit.Parent != parent.Target
                || parent.Target.Level is not (TerritoryLevel.Region or TerritoryLevel.County)
                || byTarget.ContainsKey(selection.Target)))
                throw Invalid("Choose a scope held through the selected assignment.");
            selected.Add(selection);
        }
        var result = assignments.ToDictionary(row => row.Id);
        List<string> notices = [];
        foreach (var group in selected.GroupBy(row => row.AssignmentId).OrderBy(row => row.Key))
        {
            var parent = byId[group.Key];
            var children = geography.Where(row => row.Parent == parent.Target && !byTarget.ContainsKey(row.Target)).ToArray();
            bool whole = group.Any(row => row.Target == parent.Target);
            bool last = !whole && children.Length > 0 && children.All(child => group.Any(row => row.Target == child.Target));
            if (whole || last)
            {
                result[parent.Id] = parent.CopyForTransfer(recipient);
                if (last) notices.Add($"{units[parent.Target].Name} ({parent.Target.Level}) moves to {recipientName} with its last {(parent.Target.Level == TerritoryLevel.County ? "Towns" : "Counties")}.");
            }
            else foreach (var selection in group)
            {
                // Stable planned IDs make repeated dry runs deterministic. The source assignment
                // and child scope uniquely identify a new carve-out; target uniqueness is enforced in SQL.
                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{parent.Id:D}/{selection.Target.Level}/{selection.Target.UnitId:D}"));
                var candidate = TerritoryAssignment.CreateForTransfer(new Guid(bytes.AsSpan(0, 16)), recipient, selection.Target);
                result.Add(candidate.Id, candidate);
            }
        }
        return new(result.Values.OrderBy(row => row.Id).ToArray(), notices.Order(StringComparer.Ordinal).ToArray());
    }

    private static CoverageValidationException Invalid(string message) => new("Selections", message);
}
