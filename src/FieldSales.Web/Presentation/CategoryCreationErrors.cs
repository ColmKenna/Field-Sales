using FieldSales.Web.Catalogue;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldSales.Web.Presentation;

public static class CategoryCreationErrors
{
    public static bool AddCreationError(this ModelStateDictionary state, CreateCategoryStatus status, string field)
    {
        string? message = status switch
        {
            CreateCategoryStatus.Duplicate => "A category with this name already exists here.",
            CreateCategoryStatus.Invalid => "Enter a category name of up to 200 characters.",
            _ => null
        };
        if (message is null) return false;
        state.AddModelError(field, message);
        return true;
    }
}
