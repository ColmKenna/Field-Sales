using FieldSales.Identity.Services;

namespace FieldSales.Identity.Configuration;

public class AdminConsoleOptions
{
    public const int MinPageSize = Pagination.MinPageSize;
    public const int MaxPageSize = Pagination.MaxPageSize;

    public int DefaultPageSize { get; set; } = 10;
}