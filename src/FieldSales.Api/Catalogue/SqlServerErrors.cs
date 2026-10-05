using Microsoft.Data.SqlClient;

namespace FieldSales.Api.Catalogue;

public static class SqlServerErrors
{
    public static bool IsUniqueViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 2601 or 2627 }) return true;
        return false;
    }
}
