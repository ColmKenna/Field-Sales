namespace FieldSales.Api.Catalogue;

public sealed record CategoryBreadcrumbSegment(Guid Id, string Name);

public sealed class CategoryTree
{
    private readonly Dictionary<Guid, Category> _categories;

    public CategoryTree(IEnumerable<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        _categories = categories.ToDictionary(category => category.Id);
    }

    public Category Add(string name, Guid? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string trimmedName = name.Trim();
        if (trimmedName.Length > Category.MaximumNameLength)
            throw new ArgumentException($"A category name cannot exceed {Category.MaximumNameLength} characters.",
                nameof(name));
        if (parentId is not null && !_categories.ContainsKey(parentId.Value))
            throw new ArgumentException("The parent category does not exist.", nameof(parentId));

        if (_categories.Values.Any(category => category.ParentId == parentId
                && string.Equals(category.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A category with this name already exists under this parent.");

        Category added = new(Guid.NewGuid(), parentId, trimmedName);
        _categories.Add(added.Id, added);
        return added;
    }

    public IReadOnlyList<CategoryBreadcrumbSegment> Breadcrumb(Guid categoryId)
    {
        if (!_categories.TryGetValue(categoryId, out Category? current))
            throw new KeyNotFoundException("The category does not exist.");

        List<CategoryBreadcrumbSegment> segments = [];
        HashSet<Guid> visited = [];
        while (true)
        {
            if (!visited.Add(current.Id))
                throw new InvalidOperationException("The category ancestry contains a cycle.");

            segments.Add(new CategoryBreadcrumbSegment(current.Id, current.Name));
            if (current.ParentId is null) break;
            if (!_categories.TryGetValue(current.ParentId.Value, out current))
                throw new InvalidOperationException("The category ancestry is incomplete.");
        }

        segments.Reverse();
        return segments;
    }
}
