using FieldSales.StaffAccess;

namespace FieldSales.Web.Catalogue;

public sealed partial class CatalogueApiClient
{
    public Task<CatalogueReadResult<StaffSessionResponse>> StaffSessionAsync(CancellationToken cancellationToken) =>
        ReadAsync<StaffSessionResponse>("/staff/session", cancellationToken);
}
