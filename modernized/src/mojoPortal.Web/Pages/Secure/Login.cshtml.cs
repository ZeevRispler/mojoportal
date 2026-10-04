using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using mojoPortal.Business;
using mojoPortal.Web.Services;

namespace mojoPortal.Web.Pages.Secure;

/// <summary>
/// Port of Secure/Login.aspx.cs + SiteLogin.Authenticate +
/// mojoMembershipProvider.ValidateUser (DB path). The password hash
/// algorithm is our PBKDF2 (documented deviation — legacy SHA1 hashes are
/// not portable); all the account gates and the attempt-window lockout
/// math are preserved.
/// </summary>
public class LoginModel : PageModel
{
    private readonly MojoContext _ctx;
    private readonly IUserRepository _users;
    private readonly MojoPasswordHasher _hasher;

    public LoginModel(MojoContext ctx, IUserRepository users, MojoPasswordHasher hasher)
    {
        _ctx = ctx;
        _users = users;
        _hasher = hasher;
    }

    public string? FailureMessage { get; private set; }
    public string UserNameLabel { get; private set; } = "Email";
    public string SiteRoot { get; } = string.Empty;
    private Site? _site;

    [BindProperty]
    public string? UserName { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnurl, CancellationToken ct = default)
    {
        _site = await _ctx.GetSiteAsync(ct);
        UserNameLabel = _site is { UseEmailForLogin: false } ? "User ID" : "Email";

        if (User.Identity?.IsAuthenticated == true)
        {
            var redirect = ValidatedReturnUrl(returnurl);
            if (redirect is not null) { return Redirect(redirect); }
            return Redirect("~/Default.aspx".Replace("~/", "/"));
        }

        ViewData["Title"] = _site is null ? "Login" : $"{_site.SiteName} - Login";
        ViewData["BodyClass"] = "loginpage";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnurl, CancellationToken ct = default)
    {
        _site = await _ctx.GetSiteAsync(ct);
        UserNameLabel = _site is { UseEmailForLogin: false } ? "User ID" : "Email";
        ViewData["Title"] = _site is null ? "Login" : $"{_site.SiteName} - Login";
        ViewData["BodyClass"] = "loginpage";

        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password) || _site is null)
        {
            FailureMessage = "Login Failed!";
            return Page();
        }

        var siteUser = _site.UseEmailForLogin
            ? await _users.GetByEmailAsync(_site.SiteId, UserName, ct)
            : await _users.GetByLoginNameAsync(_site.SiteId, UserName, ct);

        if (siteUser is null || siteUser.UserId <= -1 || siteUser.SiteId != _site.SiteId)
        {
            FailureMessage = "Login Failed!";
            return Page();
        }

        // SiteLogin.Authenticate gates, in source order.
        if (_site.UseSecureRegistration && siteUser.RegisterConfirmGuid != Guid.Empty)
        {
            FailureMessage = "This account has not been confirmed. You cannot login without first confirming your email address.";
            return Page();
        }

        if (siteUser.IsDeleted)
        {
            FailureMessage = "Login Failed!";
            return Page();
        }

        if (siteUser.IsLockedOut)
        {
            FailureMessage = "Sorry, this account is currently locked.";
            return Page();
        }

        if (_site.RequireApprovalBeforeLogin && !siteUser.ApprovedForLogin)
        {
            FailureMessage = "Sorry but a site administrator must approve your account before you can login.";
            return Page();
        }

        if (_site.MaxInvalidPasswordAttempts > 0
            && siteUser.FailedPasswordAttemptCount >= _site.MaxInvalidPasswordAttempts
            && siteUser.FailedPasswordAttemptWindowStart.AddMinutes(_site.PasswordAttemptWindowMinutes) > DateTime.UtcNow)
        {
            FailureMessage = "Sorry, this account is temporarily locked out due to too many failed password attempts. Please try again in a little while.";
            return Page();
        }

        var valid = siteUser.PasswordFormat == 0
            ? siteUser.Pwd == Password
            : _hasher.Verify(Password, siteUser.Pwd, siteUser.PasswordSalt);

        if (!valid)
        {
            if (_site.MaxInvalidPasswordAttempts > 0 && siteUser.UserGuid != Guid.Empty)
            {
                await _users.IncrementPasswordAttemptsAsync(
                    siteUser.UserId, _site.MaxInvalidPasswordAttempts,
                    _site.PasswordAttemptWindowMinutes, DateTime.UtcNow, ct);
            }

            FailureMessage = "Login Failed!";
            return Page();
        }

        await _users.UpdateLastLoginAsync(siteUser.UserId, DateTime.UtcNow, ct);

        var roleNames = await _users.GetRoleNamesForUserAsync(siteUser.UserId, ct);
        var claims = new List<Claim>
        {
            new("userid", siteUser.UserId.ToString()),
            new("userguid", siteUser.UserGuid.ToString()),
            new(ClaimTypes.Name, siteUser.LoginName),
        };
        claims.AddRange(roleNames.Select(r => new Claim(ClaimTypes.Role, r)));

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = false });

        var redirect = ValidatedReturnUrl(returnurl);
        return Redirect(redirect ?? "/");
    }

    /// <summary>
    /// Login.aspx.cs return-url rule: local path or site-root-prefixed URL;
    /// /accessdenied.aspx is never returned to.
    /// </summary>
    private string? ValidatedReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) { return null; }
        if (returnUrl.Contains("/accessdenied.aspx", StringComparison.OrdinalIgnoreCase)) { return null; }

        var decoded = MarkupHelper.RemoveMarkup(Uri.UnescapeDataString(returnUrl));
        // Page.ResolveUrl approximation for app-rooted paths.
        var resolved = decoded.StartsWith("~/") ? decoded[1..] : decoded;
        var siteRoot = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        if ((resolved.StartsWith('/') && !resolved.StartsWith("//"))
            || resolved.StartsWith(siteRoot, StringComparison.OrdinalIgnoreCase)
            || resolved.StartsWith(siteRoot.Replace("https://", "http://"), StringComparison.OrdinalIgnoreCase))
        {
            return decoded;
        }

        return null;
    }
}
