using FieldSales.Identity.Services;

namespace FieldSales.Identity.Configuration;

public class AdminConsoleOptions
{
    public const int MinPageSize = Pagination.MinPageSize;
    public const int MaxPageSize = Pagination.MaxPageSize;

    public int DefaultPageSize { get; set; } = 10;

    public ListQuery CreateListQuery(string? filter, int pageNumber, out int normalizedPageNumber)
    {
        ListQuery query = ListQuery.ForPage(filter, pageNumber, DefaultPageSize);
        normalizedPageNumber = query.Pagination.PageNumber;
        return query;
    }

    public Pagination CreatePagination(int pageNumber, out int normalizedPageNumber)
    {
        Pagination pagination = Pagination.From(pageNumber, DefaultPageSize);
        normalizedPageNumber = pagination.PageNumber;
        return pagination;
    }
}
