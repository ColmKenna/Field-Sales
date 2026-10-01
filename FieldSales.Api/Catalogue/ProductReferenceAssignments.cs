using System.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Api.Catalogue;

/// <summary>Transaction-safe storage boundary for later product editors; not an HTTP assignment surface.</summary>
public sealed class ProductReferenceAssignments(CatalogueDbContext db)
{
    public Task SetClassificationAsync(Guid productId, Guid? profileId, Guid? supplierId,
        CancellationToken cancellationToken = default) => CatalogueTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
    {
        var product = await db.Products.SingleAsync(product => product.Id == productId, cancellationToken);
        var profile = profileId is Guid p ? await db.ProductProfiles.SingleOrDefaultAsync(item => item.Id == p, cancellationToken) : null;
        var supplier = supplierId is Guid s ? await db.Suppliers.SingleOrDefaultAsync(item => item.Id == s, cancellationToken) : null;
        if (profileId is not null && profile is null || supplierId is not null && supplier is null)
            throw new ArgumentException("A selected reference no longer exists.");
        product.SetClassification(profile, supplier);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }, cancellationToken);

    public Task AddAttributeAsync(Guid productId, Guid nameId, string value,
        CancellationToken cancellationToken = default) => CatalogueTransactions.RunAsync(db, IsolationLevel.Serializable, async transaction =>
    {
        var product = await db.Products.Include(product => product.AttributeValues)
            .SingleAsync(product => product.Id == productId, cancellationToken);
        var name = await db.AttributeNames.SingleAsync(item => item.Id == nameId, cancellationToken);
        product.AddAttribute(name, value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }, cancellationToken);
}
