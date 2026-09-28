using FieldSales.Identity.Services.Validation;
using Microsoft.EntityFrameworkCore;

namespace FieldSales.Identity.Services.Clients;

public partial class ClientCreateService
{
    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        string message = exception.InnerException?.Message ?? exception.Message;
        bool isClientIdConstraint = message.Contains("IX_Clients_ClientId", StringComparison.OrdinalIgnoreCase)
                                    || message.Contains("Clients.ClientId", StringComparison.OrdinalIgnoreCase);

        return isClientIdConstraint && UniqueConstraintViolationDetector.IsUniqueConstraintViolation(exception);
    }
}