namespace FieldSales.Web.Catalogue;

/// <summary>The tiers a customer holds and what they would mean for its prices (WI-063, H-21), through the Field Sales API's
/// <c>/pricing/customers</c>.</summary>
public sealed partial class CatalogueApiClient
{
    private const string CustomersPricingPath = "/pricing/customers";

    public Task<CatalogueReadResult<CustomerTiersDetails>> ReadCustomerTiersAsync(Guid customerId, CancellationToken cancellationToken) =>
        ReadAsync<CustomerTiersDetails>($"{CustomersPricingPath}/{customerId}/tiers", cancellationToken);

    /// <summary>The sample for <paramref name="tierIds"/> (ticked, saved or not), with any products matching <paramref name="find"/>.</summary>
    public Task<CatalogueReadResult<CustomerPriceSample>> CustomerPriceSampleAsync(Guid customerId, IEnumerable<Guid> tierIds,
        string? find, CancellationToken cancellationToken)
    {
        IEnumerable<string> query = tierIds.Select(id => $"tierId={id}");
        if (!string.IsNullOrWhiteSpace(find)) query = query.Append($"find={Uri.EscapeDataString(find.Trim())}");
        string path = $"{CustomersPricingPath}/{customerId}/sample";
        return ReadAsync<CustomerPriceSample>(query.Any() ? $"{path}?{string.Join("&", query)}" : path, cancellationToken);
    }

    /// <summary>Replaces the customer's tiers with <paramref name="tierIds"/>.</summary>
    public async Task<PriceTierSaveResult> SaveCustomerTiersAsync(Guid customerId, IReadOnlyList<Guid> tierIds,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, $"{CustomersPricingPath}/{customerId}/tiers",
            new SetCustomerTiersRequest(tierIds), cancellationToken);
        return await SavedAsync(response, cancellationToken);
    }
}
