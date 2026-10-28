using FieldSales.Identity.Services.Clients;

namespace FieldSales.Identity.Presentation;

public static class FormPresentation
{
    public static bool MatchesConfirmation(string? entered, string expected) =>
        string.Equals(entered?.Trim(), expected, StringComparison.Ordinal);

    public static int ValidationTab(IEnumerable<string> errorKeys, params string[] secondTabPrefixes) =>
        errorKeys.Any(key => secondTabPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal))) ? 1 : 0;

    public static bool TryGetClientId(string? id, out ClientId clientId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            clientId = default;
            return false;
        }

        clientId = ClientId.Create(id);
        return true;
    }
}
