using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Events;
using FieldSales.Identity.Data;
using FieldSales.Identity.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace FieldSales.Identity.Admin.Tests.Pages.Account;

public sealed class StaffLoginFailureTests
{
    [Fact]
    public async Task RejectedStaffCredentialsReturnToStaffEntryForRetry()
    {
        var users = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signIn = new Mock<SignInManager<ApplicationUser>>(users.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);
        signIn.Setup(s => s.PasswordSignInAsync("staff@sales.local", "incorrect", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        var interaction = new Mock<IIdentityServerInteractionService>();
        interaction.Setup(i => i.GetAuthorizationContextAsync("/connect/authorize/callback", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationRequest
            {
                Client = new Client { ClientId = "fieldsales-staff-web" },
                RedirectUri = "https://localhost:7203/signin-oidc"
            });
        LoginModel page = new(signIn.Object, interaction.Object, new Mock<IEventService>().Object)
        {
            Input = new LoginModel.InputModel { Username = "staff@sales.local", Password = "incorrect" },
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

        IActionResult result = await page.OnPostAsync("/connect/authorize/callback");

        Assert.Equal("https://localhost:7203/?error=sign-in", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public async Task RejectedNonStaffCredentialsRemainOnIdentityLogin()
    {
        var users = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signIn = new Mock<SignInManager<ApplicationUser>>(users.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);
        signIn.Setup(s => s.PasswordSignInAsync("admin@sales.local", "incorrect", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        var interaction = new Mock<IIdentityServerInteractionService>();
        interaction.Setup(i => i.GetAuthorizationContextAsync("/connect/authorize/callback", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationRequest
            {
                Client = new Client { ClientId = "other-client" },
                RedirectUri = "https://other.example/signin-oidc"
            });
        LoginModel page = new(signIn.Object, interaction.Object, new Mock<IEventService>().Object)
        {
            Input = new LoginModel.InputModel { Username = "admin@sales.local", Password = "incorrect" },
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

        Assert.IsType<PageResult>(await page.OnPostAsync("/connect/authorize/callback"));
        Assert.Equal("Invalid username or password.", page.ErrorMessage);
    }
}
