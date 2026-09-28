global using FieldSales.Identity.Services;
using FieldSales.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace FieldSales.Identity.Admin.Tests.Infrastructure;

public static class TestOptions
{
    public const int PageSize = 10;

    public static IOptions<AdminConsoleOptions> AdminConsole { get; } =
        Options.Create(new AdminConsoleOptions
        {
            DefaultPageSize = PageSize
        });
}