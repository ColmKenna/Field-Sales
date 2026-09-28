namespace FieldSales.Identity.Services.Clients;

public interface IClientPresetService
{
    IReadOnlyList<ClientPreset> GetAvailablePresets();
    ClientPreset? GetPreset(string id);
}