using System.Globalization;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Coverage;

public static class RestrictionPermissionFormatter
{
    public static RestrictionPermissionChangeDetails Details(RestrictionPermissionChange row)
    {
        string timestamp = row.ChangedAt.ToUniversalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
        string change = row.Change switch
        {
            RestrictionPermissionChangeKind.Granted => "Granted",
            RestrictionPermissionChangeKind.Removed => "Removed",
            _ => throw new InvalidOperationException("Unknown permission change.")
        };
        string display = $"{change} {timestamp} UTC by {row.ActorName}";
        if (row.Reason is not null) display += " — " + row.Reason;
        return new(row.Id, row.Sequence, new(row.RepSubject, row.RepName), row.RestrictionGroupId, row.RestrictionGroupName,
            row.Change, row.ChangedAt, new(row.ActorSubject, row.ActorName), row.Reason, display);
    }
}
