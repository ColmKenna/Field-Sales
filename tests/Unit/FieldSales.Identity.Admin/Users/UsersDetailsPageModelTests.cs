using System.Security.Claims;
using FieldSales.Identity.Pages.Admin.Users;
using FieldSales.Identity.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace FieldSales.Identity.Admin.Tests.Users;

[Trait("Category", "Unit")]

public class UsersDetailsPageModelTests
{
    private static DetailsModel CreateModel(Mock<IUserDetailsService> service, string? currentUserId = "admin")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            currentUserId == null
                ? Array.Empty<Claim>()
                : new[] { new Claim(ClaimTypes.NameIdentifier, currentUserId) },
            "test"));

        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var pageContext = new PageContext(actionContext)
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        };

        return new DetailsModel(service.Object)
        {
            PageContext = pageContext,
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };
    }

    [Fact]
    public async Task OnPostUnlockAsync_FailureWithoutServiceErrors_RedirectsWithDefaultErrorMessage()
    {
        var service = new Mock<IUserDetailsService>();
        service.Setup(s => s.UnlockUserAsync(UserId.Create("user-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserUnlockResult(UserUnlockStatus.Failed, Array.Empty<string>()));
        DetailsModel model = CreateModel(service);
        model.Id = "user-1";

        IActionResult result = await model.OnPostUnlockAsync(CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("overview", redirect.RouteValues!["tab"]);
        Assert.Equal("Unable to unlock the user account.", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAddRoleAsync_MissingRole_ReturnsNotFound()
    {
        var service = new Mock<IUserDetailsService>();
        service.Setup(s => s.AddRoleAsync(UserId.Create("user-1"), "missing-role", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RoleChangeResult.Failed("Role not found.", status: FieldSales.Identity.Services.Validation.AdminMutationStatus.NotFound));
        DetailsModel model = CreateModel(service);
        model.Id = "user-1";

        IActionResult result = await model.OnPostAddRoleAsync("missing-role", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostRevokeUserAccessAsync_ServiceFailure_RedirectsToAccessWithServiceError()
    {
        var service = new Mock<IUserDetailsService>();
        service.Setup(s =>
                s.RevokeUserAccessAsync(new UserActionContext(UserId.Create("user-1"), UserId.Create("admin")),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserAccessRevokeResult.Failed("The current administrator cannot revoke their own access."));
        DetailsModel model = CreateModel(service);
        model.Id = "user-1";

        IActionResult result = await model.OnPostRevokeUserAccessAsync(CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("access", redirect.RouteValues!["tab"]);
        Assert.Equal("The current administrator cannot revoke their own access.", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostDeleteAsync_InvalidConfirmation_RedirectsWithoutCallingService()
    {
        var service = new Mock<IUserDetailsService>();
        DetailsModel model = CreateModel(service);
        model.Id = "user-1";
        model.DeleteConfirmation = "delete";

        IActionResult result = await model.OnPostDeleteAsync(CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("danger", redirect.RouteValues!["tab"]);
        Assert.Equal("Type DELETE exactly to confirm permanent deletion.", model.ErrorMessage);
        service.Verify(s => s.DeleteUserAsync(It.IsAny<UserActionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnGetAsync_PreparesUnassignedRoles_InAllRolesOrder()
    {
        var service = new Mock<IUserDetailsService>();
        service.Setup(s => s.GetUserDetailsAsync(It.IsAny<UserActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDetailsModel
            {
                Id = "user-1",
                UserName = "jane",
                AllRoles = new List<string> { "SysAdmin", "Support", "Auditor", "Billing" },
                AssignedRoles = new List<string> { "Support", "Billing" }
            });

        DetailsModel model = CreateModel(service);
        model.Id = "user-1";

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(new[] { "SysAdmin", "Auditor" }, model.UnassignedRoles);
    }

    [Fact]
    public async Task OnGetAsync_WithEveryRoleAssigned_OffersNone()
    {
        var service = new Mock<IUserDetailsService>();
        service.Setup(s => s.GetUserDetailsAsync(It.IsAny<UserActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDetailsModel
            {
                Id = "user-1",
                UserName = "jane",
                AllRoles = new List<string> { "SysAdmin" },
                AssignedRoles = new List<string> { "SysAdmin" }
            });

        DetailsModel model = CreateModel(service);
        model.Id = "user-1";

        await model.OnGetAsync(CancellationToken.None);

        Assert.Empty(model.UnassignedRoles);
    }

    [Theory]
    [InlineData("AddRole", true)] [InlineData("AddRole", false)]
    [InlineData("RemoveRole", true)] [InlineData("RemoveRole", false)]
    [InlineData("AddClaim", true)] [InlineData("AddClaim", false)]
    [InlineData("RemoveClaim", true)] [InlineData("RemoveClaim", false)]
    [InlineData("Revoke", true)] [InlineData("Revoke", false)]
    [InlineData("Suspend", true)] [InlineData("Suspend", false)]
    [InlineData("Delete", true)] [InlineData("Delete", false)]
    public async Task MutationResponse_UsesStatusRegardlessOfMessage(string handler, bool missing)
    {
        var service = new Mock<IUserDetailsService>();
        var status = missing ? FieldSales.Identity.Services.Validation.AdminMutationStatus.NotFound
            : FieldSales.Identity.Services.Validation.AdminMutationStatus.Denied;
        const string message = "A revised explanation.";
        service.Setup(s => s.AddRoleAsync(It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RoleChangeResult.Failed(message, status: status));
        service.Setup(s => s.RemoveRoleAsync(It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RoleChangeResult.Failed(message, status: status));
        service.Setup(s => s.AddClaimAsync(It.IsAny<UserId>(), It.IsAny<UserClaim>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClaimChangeResult.Failed(message, status));
        service.Setup(s => s.RemoveClaimAsync(It.IsAny<UserId>(), It.IsAny<UserClaim>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClaimChangeResult.Failed(message, status));
        service.Setup(s => s.RevokeUserAccessAsync(It.IsAny<UserActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserAccessRevokeResult.Failed(message, status));
        service.Setup(s => s.SuspendUserAsync(It.IsAny<UserActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSuspendResult.Failed(message, status));
        service.Setup(s => s.DeleteUserAsync(It.IsAny<UserActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserDeleteResult.Failed(message, status));
        DetailsModel model = CreateModel(service);
        model.Id = "user-1";
        model.DeleteConfirmation = "DELETE";
        IActionResult result = handler switch
        {
            "AddRole" => await model.OnPostAddRoleAsync("Support", CancellationToken.None),
            "RemoveRole" => await model.OnPostRemoveRoleAsync("Support", CancellationToken.None),
            "AddClaim" => await model.OnPostAddClaimAsync("review_claim", "value", CancellationToken.None),
            "RemoveClaim" => await model.OnPostRemoveClaimAsync("review_claim", "value", CancellationToken.None),
            "Revoke" => await model.OnPostRevokeUserAccessAsync(CancellationToken.None),
            "Suspend" => await model.OnPostSuspendAsync(CancellationToken.None),
            _ => await model.OnPostDeleteAsync(CancellationToken.None)
        };
        if (missing) Assert.IsType<NotFoundResult>(result);
        else
        {
            var redirect = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal(handler.Contains("Role") ? "roles" : handler.Contains("Claim") ? "claims"
                : handler == "Revoke" ? "access" : "danger", redirect.RouteValues!["tab"]);
            Assert.Equal(message, model.ErrorMessage);
        }
    }
}
