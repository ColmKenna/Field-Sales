using System.Text.Json;
using FieldSales.Api.Catalogue;
using FieldSales.Api.Directory;
using FieldSales.Api.Ordering;
using FieldSales.Ordering.Contracts;
using FieldSales.Pricing;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Pricing;

/// <summary>Tiers on the customer record (WI-063, H-21): which tiers a customer holds, saving them as a set, and a sample of what a set
/// of tiers would mean for the customer's prices, worked out by the engine before anything is saved. Head Office Users only.</summary>
public static class CustomerTierEndpoints
{
    /// <summary>How many of the customer's most-ordered products the sample shows, from how many recent accepted orders (D3).</summary>
    public const int SampleSize = 5;
    public const int RecentOrders = 20;
    public const string UnknownTierMessage = "Choose tiers from the list.";

    public static void MapCustomerTierEndpoints(this WebApplication app)
    {
        RouteGroupBuilder customers = app.MapGroup("/pricing/customers").RequireAuthorization(PriceTierEndpoints.Policy);
        customers.MapGet("/{customerId:guid}/tiers", ReadAsync);
        customers.MapPut("/{customerId:guid}/tiers", SetAsync);
        customers.MapGet("/{customerId:guid}/sample", SampleAsync);
    }

    private static async Task<IResult> ReadAsync(Guid customerId, DirectoryDbContext directory, CatalogueDbContext catalogue,
        CustomerTierPricing pricing, CancellationToken cancellationToken)
    {
        if (!await directory.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken)) return Results.NotFound();
        PriceTierListItem[] tiers = await catalogue.PriceTiers.AsNoTracking().OrderBy(tier => tier.Name)
            .Select(tier => new PriceTierListItem(tier.Id, tier.Name, tier.PercentOff, tier.Overrides.Count,
                tier.Shape == PriceTierShape.PriceList ? PriceTierShapes.PriceList : PriceTierShapes.Percentage))
            .ToArrayAsync(cancellationToken);
        return Results.Ok(new CustomerTiersDetails(customerId, await pricing.TierIdsAsync(customerId, cancellationToken), tiers));
    }

    // D2: the set replaces what the customer held; a tier sent twice is held once.
    private static async Task<IResult> SetAsync(Guid customerId, SetCustomerTiersRequest request, DirectoryDbContext directory,
        CatalogueDbContext catalogue, CancellationToken cancellationToken)
    {
        if (!await directory.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken)) return Results.NotFound();
        if (request.TierIds is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["TierIds"] = ["Send the customer's tiers."] });
        Guid[] wanted = [.. request.TierIds.Distinct()];
        if (await catalogue.PriceTiers.CountAsync(tier => wanted.Contains(tier.Id), cancellationToken) != wanted.Length)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["TierIds"] = [UnknownTierMessage] });
        List<CustomerPriceTier> held = await catalogue.CustomerPriceTiers.Where(link => link.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        catalogue.CustomerPriceTiers.RemoveRange(held.Where(link => !wanted.Contains(link.PriceTierId)));
        catalogue.CustomerPriceTiers.AddRange(wanted.Where(id => !held.Any(link => link.PriceTierId == id))
            .Select(id => new CustomerPriceTier(customerId, id)));
        try
        {
            await catalogue.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException || SqlServerErrors.IsUniqueViolation(exception))
        {
            return Results.Conflict(new ProductSaveError(string.Empty, "This customer's tiers changed while you were saving. Reload them and try again."));
        }
        return Results.NoContent();
    }

    // D3: the customer's most-ordered products and any searched for, priced with the tiers ticked (saved or not) for today.
    private static async Task<IResult> SampleAsync(Guid customerId, Guid[]? tierId, string? find, DirectoryDbContext directory,
        OrderingDbContext ordering, CatalogueDbContext catalogue, CustomerTierPricing pricing, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        Guid[] locations = await directory.Locations.AsNoTracking().Where(location => location.CustomerId == customerId)
            .Select(location => location.Id).ToArrayAsync(cancellationToken);
        if (locations.Length == 0 && !await directory.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken))
            return Results.NotFound();
        FieldSales.Pricing.PriceTier[] tiers = await pricing.EngineTiersAsync([.. (tierId ?? []).Distinct()], cancellationToken);
        DateOnly today = BusinessCalendar.Today(clock.GetUtcNow());

        Dictionary<Guid, int> orderCounts = await OrderCountsAsync(ordering, locations, cancellationToken);
        Guid[] counted = [.. orderCounts.Keys];
        Guid[] mostOrdered = [.. (await catalogue.Products.AsNoTracking().Where(product => counted.Contains(product.Id))
                .Select(product => new { product.Id, product.Name }).ToListAsync(cancellationToken))
            .OrderByDescending(product => orderCounts[product.Id]).ThenBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(SampleSize).Select(product => product.Id)];
        string search = find?.Trim() ?? string.Empty;
        IQueryable<Product> matches = catalogue.Products.AsNoTracking()
            .Where(product => search.Length > 0 && (product.Code.Contains(search) || product.Name.Contains(search)));
        int foundCount = await matches.CountAsync(cancellationToken);
        Guid[] found = await matches.OrderBy(product => product.Name).ThenBy(product => product.Code).Take(SampleSize)
            .Select(product => product.Id).ToArrayAsync(cancellationToken);

        Guid[] wanted = [.. mostOrdered.Union(found)];
        Dictionary<Guid, Product> products = await pricing.PricedProductsQuery().Where(product => wanted.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        CustomerPriceSampleRow[] Rows(IEnumerable<Guid> ids) =>
            [.. ids.Where(products.ContainsKey).Select(id => Row(products[id], tiers, today))];
        return Results.Ok(new CustomerPriceSample(Rows(mostOrdered), Rows(found), foundCount));
    }

    /// <summary>For each product on the Locations' last <see cref="RecentOrders"/> accepted orders, how many of those orders it is on
    /// (D3). Pending orders don't count. The lines are read from the orders as received.</summary>
    private static async Task<Dictionary<Guid, int>> OrderCountsAsync(OrderingDbContext ordering, Guid[] locations,
        CancellationToken cancellationToken)
    {
        if (locations.Length == 0) return [];
        string[] bodies = await (from order in ordering.Orders.AsNoTracking()
                                 where order.Status == OrderLifecycleStatus.Accepted && locations.Contains(order.LocationId)
                                 orderby order.TakenOn descending, order.ReceivedUtc descending, order.Id descending
                                 join work in ordering.ReceivedWork on order.Id equals work.Id
                                 select work.Body).Take(RecentOrders).ToArrayAsync(cancellationToken);
        return bodies.Select(body => JsonSerializer.Deserialize(body, OrderJsonContext.Default.TabletOrder)!)
            .SelectMany(order => order.Lines.Select(line => line.ProductId).Distinct())
            .GroupBy(product => product)
            .ToDictionary(product => product.Key, product => product.Count());
    }

    // At the smallest quantity the product can be ordered in: 1 for Each, its minimum for a measure.
    private static CustomerPriceSampleRow Row(Product product, IReadOnlyList<FieldSales.Pricing.PriceTier> tiers, DateOnly today)
    {
        decimal quantity = product.MinimumQuantity ?? 1m;
        PricedProduct priced = product.ToPricedProduct();
        ResolvedPrice? price = PriceResolver.Resolve(priced, tiers, quantity, today);
        return new(product.Id, product.Code, product.Name, product.Unit, quantity, priced.ListPriceOn(today), price?.Winner.Amount,
            price?.Winner.Label);
    }
}
