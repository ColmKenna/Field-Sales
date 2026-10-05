namespace FieldSales.Api.Catalogue;

public sealed record CategoryProductCount(int Here, int Beneath);

public static class CategoryProductCounts
{
    // Fold leaves into parents once, without recursion or a database query per category.
    public static IReadOnlyDictionary<Guid, CategoryProductCount> Calculate(
        IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, int> directCounts)
    {
        Dictionary<Guid, Category> byId = categories.ToDictionary(category => category.Id);
        Dictionary<Guid, int> totals = categories.ToDictionary(category => category.Id,
            category => directCounts.GetValueOrDefault(category.Id));
        Dictionary<Guid, int> remainingChildren = categories.ToDictionary(category => category.Id, _ => 0);
        foreach (Category category in categories)
        {
            if (category.ParentId is not Guid parent) continue;
            if (!byId.ContainsKey(parent))
                throw new InvalidOperationException("The category ancestry is incomplete.");
            remainingChildren[parent]++;
        }

        Queue<Guid> leaves = new(remainingChildren.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        int visited = 0;
        while (leaves.TryDequeue(out Guid id))
        {
            visited++;
            if (byId[id].ParentId is not Guid parent) continue;
            totals[parent] = checked(totals[parent] + totals[id]);
            if (--remainingChildren[parent] == 0) leaves.Enqueue(parent);
        }
        if (visited != categories.Count)
            throw new InvalidOperationException("The category ancestry contains a cycle.");

        return categories.ToDictionary(category => category.Id,
            category => new CategoryProductCount(directCounts.GetValueOrDefault(category.Id), totals[category.Id]));
    }
}
