namespace FieldSales.Identity.Services.Users;

public static class UserActionPolicy
{
    public static bool IsSelf(string? actorId, string targetId) =>
        !string.IsNullOrWhiteSpace(actorId) && string.Equals(actorId, targetId, StringComparison.Ordinal);
}
