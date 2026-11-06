using FieldSales.Api.Catalogue;
using FieldSales.Pricing;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Pricing;

/// <summary>Head office's price tiers (WI-060, WI-061, H-20): list, create a percentage tier or a price list, rename or re-percentage,
/// change shape, and set or remove a product's override or listed price. Tiers aren't deleted (WI-060 D5). Head Office Users only
/// (D9).</summary>
public static class PriceTierEndpoints
{
    public const string Policy = "HeadOfficePricing";

    public static void MapPriceTierEndpoints(this WebApplication app)
    {
        RouteGroupBuilder tiers = app.MapGroup("/pricing/tiers").RequireAuthorization(Policy);
        tiers.MapGet("/", ListAsync);
        tiers.MapPost("/", CreateAsync);
        tiers.MapGet("/{id:guid}", ReadAsync);
        tiers.MapPut("/{id:guid}", ChangeAsync);
        tiers.MapPut("/{id:guid}/shape", ChangeShapeAsync);
        tiers.MapPut("/{id:guid}/overrides/{productId:guid}", SetOverrideAsync);
        tiers.MapDelete("/{id:guid}/overrides/{productId:guid}", RemoveOverrideAsync);
    }

    private static async Task<PriceTierListItem[]> ListAsync(CatalogueDbContext db, CancellationToken cancellationToken) =>
        await db.PriceTiers.AsNoTracking().OrderBy(tier => tier.Name)
            .Select(tier => new PriceTierListItem(tier.Id, tier.Name, tier.PercentOff, tier.Overrides.Count,
                tier.Shape == PriceTierShape.PriceList ? PriceTierShapes.PriceList : PriceTierShapes.Percentage))
            .ToArrayAsync(cancellationToken);

    private static async Task<IResult> CreateAsync(SavePriceTierRequest request, CatalogueDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.Shape is not null && !PriceTierShapes.IsKnown(request.Shape))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Shape"] = [UnknownShapeMessage] });
        PriceTierShape shape = request.Shape == PriceTierShapes.PriceList ? PriceTierShape.PriceList : PriceTierShape.Percentage;
        if (Invalid(request, shape) is { } errors) return Results.ValidationProblem(errors);
        string name = request.Name!.Trim();
        if (await NamedAsync(db, name, null, cancellationToken) is { } taken) return Duplicate(taken);
        PriceTier tier = shape == PriceTierShape.PriceList
            ? PriceTier.CreatePriceList(name)
            : PriceTier.CreatePercentage(name, request.PercentOff!.Value);
        db.PriceTiers.Add(tier);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        {
            // The unique index settles two saves of the same name at once.
            return Duplicate((await NamedAsync(db, name, tier.Id, cancellationToken))!);
        }
        return Results.Created($"/pricing/tiers/{tier.Id}", new PriceTierListItem(tier.Id, tier.Name, tier.PercentOff, 0, ShapeName(tier.Shape)));
    }

    private static async Task<IResult> ReadAsync(Guid id, CatalogueDbContext db, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        PriceTier? tier = await db.PriceTiers.AsNoTracking().Include(row => row.Overrides)
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (tier is null) return Results.NotFound();
        Guid[] productIds = [.. tier.Overrides.Select(entry => entry.ProductId)];
        Dictionary<Guid, Product> products = await db.Products.AsNoTracking().Include(product => product.BasePrices)
            .Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);
        DateOnly today = BusinessCalendar.Today(clock.GetUtcNow());
        FieldSales.Pricing.PriceTier[] engineTier = [tier.ToEngine()];
        PriceTierOverrideItem[] overrides = [.. tier.Overrides.Select(entry =>
        {
            Product product = products[entry.ProductId];
            PricedProduct priced = product.ToPricedProduct();
            // What the tier gives today, and whether a fixed price is above today's list price, worked out now and never stored
            // (WI-060 D6, WI-061 D3).
            decimal? tierPrice = TierCandidates.For(priced, engineTier, today).FirstOrDefault()?.Amount;
            decimal? listPrice = priced.ListPriceOn(today);
            return new PriceTierOverrideItem(product.Id, product.Code, product.Name, product.Unit, entry.PercentOff, entry.FixedPrice,
                listPrice, tierPrice, entry.SetOn, entry.IsAboveList(listPrice));
        }).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.Code, StringComparer.Ordinal)];
        return Results.Ok(new PriceTierDetails(tier.Id, tier.Name, tier.PercentOff, overrides, ShapeName(tier.Shape)));
    }

    private static async Task<IResult> ChangeAsync(Guid id, SavePriceTierRequest request, CatalogueDbContext db,
        CancellationToken cancellationToken)
    {
        PriceTier? tier = await db.PriceTiers.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (tier is null) return Results.NotFound();
        if (Invalid(request, tier.Shape) is { } errors) return Results.ValidationProblem(errors);
        string name = request.Name!.Trim();
        if (await NamedAsync(db, name, id, cancellationToken) is { } taken) return Duplicate(taken);
        tier.Change(name, request.PercentOff);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        {
            return Duplicate((await NamedAsync(db, name, id, cancellationToken))!);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> SetOverrideAsync(Guid id, Guid productId, SetPriceTierOverrideRequest request,
        CatalogueDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        PriceTier? tier = await db.PriceTiers.Include(row => row.Overrides).SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (tier is null || !await db.Products.AnyAsync(product => product.Id == productId, cancellationToken)) return Results.NotFound();
        if (tier.Shape == PriceTierShape.PriceList && request.PercentOff is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["PercentOff"] = [PriceTier.PriceListTermMessage] });
        if (Term(request, out TierTerm? term) is { } errors) return Results.ValidationProblem(errors);
        tier.SetOverride(productId, term!, BusinessCalendar.Today(clock.GetUtcNow()));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        {
            return Results.Conflict(new ProductSaveError(string.Empty, "This tier changed while you were saving. Reload it and try again."));
        }
        return Results.NoContent();
    }

    // WI-061 D1, D4: to a price list, percentage overrides go and fixed ones stay as listed prices; to a percentage tier, listed
    // prices stay as fixed overrides. The website shows what will change before it sends this (D2).
    private static async Task<IResult> ChangeShapeAsync(Guid id, ChangePriceTierShapeRequest request, CatalogueDbContext db,
        CancellationToken cancellationToken)
    {
        PriceTier? tier = await db.PriceTiers.Include(row => row.Overrides).SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (tier is null) return Results.NotFound();
        if (!PriceTierShapes.IsKnown(request.Shape))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Shape"] = [UnknownShapeMessage] });
        PriceTierShape target = request.Shape == PriceTierShapes.PriceList ? PriceTierShape.PriceList : PriceTierShape.Percentage;
        if (target == tier.Shape)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Shape"] = [$"{tier.Name} is already {(target == PriceTierShape.PriceList ? "a price list" : "a percentage tier")}."]
            });
        if (target == PriceTierShape.PriceList)
        {
            if (request.PercentOff is not null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["PercentOff"] = [PriceTier.PriceListPercentMessage] });
            tier.ChangeToPriceList();
        }
        else
        {
            if (request.PercentOff is not decimal percent || !TierTerms.IsValidPercent(percent))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["PercentOff"] = [TierTerms.PercentMessage] });
            tier.ChangeToPercentage(percent);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveOverrideAsync(Guid id, Guid productId, CatalogueDbContext db,
        CancellationToken cancellationToken)
    {
        PriceTier? tier = await db.PriceTiers.Include(row => row.Overrides).SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (tier is null || !tier.RemoveOverride(productId)) return Results.NotFound();
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private const string UnknownShapeMessage = "Choose a percentage tier or a price list.";

    private static string ShapeName(PriceTierShape shape) =>
        shape == PriceTierShape.PriceList ? PriceTierShapes.PriceList : PriceTierShapes.Percentage;

    private static Dictionary<string, string[]>? Invalid(SavePriceTierRequest request, PriceTierShape shape)
    {
        Dictionary<string, string[]> errors = [];
        if (!NameRules.IsValid(request.Name, PriceTier.MaximumNameLength)) errors["Name"] = [PriceTier.NameMessage];
        if (shape == PriceTierShape.PriceList)
        {
            if (request.PercentOff is not null) errors["PercentOff"] = [PriceTier.PriceListPercentMessage];
        }
        else if (request.PercentOff is not decimal percent || !TierTerms.IsValidPercent(percent))
        {
            errors["PercentOff"] = [TierTerms.PercentMessage];
        }
        return errors.Count == 0 ? null : errors;
    }

    private static Dictionary<string, string[]>? Term(SetPriceTierOverrideRequest request, out TierTerm? term)
    {
        term = null;
        string? error = (request.PercentOff, request.FixedPrice) switch
        {
            (decimal percent, null) when TierTerms.IsValidPercent(percent) => null,
            (decimal, null) => TierTerms.PercentMessage,
            (null, decimal price) when TierTerms.IsValidFixedPrice(price) => null,
            (null, decimal) => TierTerms.FixedPriceMessage,
            _ => "Enter either a percentage off or a fixed price."
        };
        if (error is not null)
            return new() { [request.FixedPrice is not null && request.PercentOff is null ? "FixedPrice" : "PercentOff"] = [error] };
        term = request.PercentOff is decimal off ? TierTerm.Percent(off) : TierTerm.Fixed(request.FixedPrice!.Value);
        return null;
    }

    private static Task<PriceTier?> NamedAsync(CatalogueDbContext db, string name, Guid? except, CancellationToken cancellationToken) =>
        // The column's collation ignores case, so "tier b" finds "Tier B".
        db.PriceTiers.AsNoTracking().FirstOrDefaultAsync(tier => tier.Name == name && tier.Id != except, cancellationToken);

    private static IResult Duplicate(PriceTier taken) =>
        Results.Conflict(new ProductSaveError("Name", $"A tier called {taken.Name} already exists."));
}
