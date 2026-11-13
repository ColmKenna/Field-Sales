using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using FieldSales.Identity;
using FieldSales.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldSales.Identity.Pages.Account;

// Reachable before sign-in by definition; exempt from the fallback policy.
[AllowAnonymous]
public class LogoutModel(
    SignInManager<ApplicationUser> signInManager,
    IIdentityServerInteractionService interaction,
    IEventService events) : PageModel
{
    private readonly IEventService events = events;
    private readonly IIdentityServerInteractionService interaction = interaction;
    private readonly SignInManager<ApplicationUser> signInManager = signInManager;

    [BindProperty] public string? LogoutId { get; set; }

    public string? PostLogoutRedirectUri { get; set; }

    public string? SignOutIFrameUrl { get; set; }

    public bool ShowLogoutPrompt { get; set; } = true;
    public bool ReturnToStaff { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? logoutId)
    {
        LogoutId = logoutId;

        if (logoutId is not null)
        {
            LogoutRequest? logoutContext =
                await interaction.GetLogoutContextAsync(logoutId, HttpContext.RequestAborted);
            if (logoutContext?.ShowSignoutPrompt == false) return await OnPostAsync();
        }

        if (User?.Identity?.IsAuthenticated == true)
        {
            ShowLogoutPrompt = true;
            return Page();
        }

        ShowLogoutPrompt = false;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            await events.RaiseAsync(
                new UserLogoutSuccessEvent(User.GetSubjectId(), User.GetDisplayName()),
                HttpContext.RequestAborted);
            await signInManager.SignOutAsync();
        }

        if (LogoutId is not null)
        {
            LogoutRequest? logoutContext =
                await interaction.GetLogoutContextAsync(LogoutId, HttpContext.RequestAborted);
            PostLogoutRedirectUri = logoutContext?.PostLogoutRedirectUri;
            SignOutIFrameUrl = logoutContext?.SignOutIFrameUrl;
            ReturnToStaff = logoutContext?.ClientId == Config.StaffWebClientId;
        }

        // If client applications have front-channel logout URLs registered, we must render
        // the page so the browser loads the notification iframe before leaving.
        if (!string.IsNullOrEmpty(SignOutIFrameUrl))
        {
            ShowLogoutPrompt = false;
            return Page();
        }

        if (PostLogoutRedirectUri is not null) return Redirect(PostLogoutRedirectUri);

        ShowLogoutPrompt = false;
        return Page();
    }
}
