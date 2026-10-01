using Microsoft.AspNetCore.Mvc;

namespace FieldSales.Web.Catalogue;

public enum CatalogueReadStatus { Found, Missing, Unavailable, Unauthorized, Forbidden }
public sealed record CatalogueReadResult<T>(CatalogueReadStatus Status, T? Value = default)
{
    public bool Found => Status == CatalogueReadStatus.Found;
    public IActionResult FailureResult() => Status switch
    {
        CatalogueReadStatus.Missing => new NotFoundResult(),
        CatalogueReadStatus.Unauthorized => new ChallengeResult(),
        CatalogueReadStatus.Forbidden => new ForbidResult(),
        CatalogueReadStatus.Unavailable => new StatusCodeResult(StatusCodes.Status503ServiceUnavailable),
        _ => throw new InvalidOperationException("A successful read has no failure response.")
    };
}
