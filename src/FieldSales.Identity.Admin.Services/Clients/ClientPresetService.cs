namespace FieldSales.Identity.Services.Clients;

public class ClientPresetService : IClientPresetService
{
    private static readonly List<ClientPreset> _presets = new()
    {
        new ClientPreset(
            ClientPresetIds.Web,
            "Web Application",
            "Server-side web application (e.g., ASP.NET Core MVC).",
            "🌐",
            true,
            true,
            [Duende.IdentityModel.OidcConstants.GrantTypes.AuthorizationCode],
            ["openid", "profile"]),

        new ClientPreset(
            ClientPresetIds.SpaBff,
            "Single Page App (BFF)",
            "Single page application using Backend-for-Frontend pattern.",
            "🛡️",
            true,
            true,
            [Duende.IdentityModel.OidcConstants.GrantTypes.AuthorizationCode],
            ["openid", "profile"]),

        new ClientPreset(
            ClientPresetIds.SpaBrowser,
            "Single Page App (Browser)",
            "Single page application running entirely in the browser.",
            "💻",
            true,
            false,
            [Duende.IdentityModel.OidcConstants.GrantTypes.AuthorizationCode],
            ["openid", "profile"]),

        new ClientPreset(
            ClientPresetIds.MachineToMachine,
            "Machine to Machine",
            "Non-interactive application or background service.",
            "⚙️",
            false,
            true,
            [Duende.IdentityModel.OidcConstants.GrantTypes.ClientCredentials],
            [])
    };

    public IReadOnlyList<ClientPreset> GetAvailablePresets() => _presets;

    public ClientPreset? GetPreset(string id) => _presets.FirstOrDefault(p => p.Id == id);
}