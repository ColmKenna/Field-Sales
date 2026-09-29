namespace FieldSales.Api.Catalogue;

public sealed class Category
{
    // EF Core materializes persisted categories through this constructor.
    private Category() { }

    internal Category(Guid id, Guid? parentId, string name)
    {
        Id = id;
        ParentId = parentId;
        Name = name;
    }

    public Guid Id { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
