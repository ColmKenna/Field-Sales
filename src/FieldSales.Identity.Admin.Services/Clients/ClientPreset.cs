namespace FieldSales.Identity.Services.Clients;

public sealed record ClientPreset(
    string Id,
    string Name,
    string Description,
    string Icon,
    bool RequirePkce,
    bool RequireClientSecret,
    IReadOnlyList<string> GrantTypes,
    IReadOnlyList<string> AllowedScopes);