using System.Security.Claims;
using mojoPortal.Business.Authorization;

namespace mojoPortal.Web.Services;

/// <summary>
/// Adapts the cookie-authenticated ClaimsPrincipal to the mojoPortal user
/// abstraction. Role names live in standard role claims (e.g. "Admins",
/// "Content Administrators").
/// </summary>
public sealed class ClaimsMojoUser : IMojoUser
{
    private readonly ClaimsPrincipal? _principal;

    public ClaimsMojoUser(IHttpContextAccessor accessor)
        => _principal = accessor.HttpContext?.User;

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated == true;

    public int UserId
    {
        get
        {
            var value = _principal?.FindFirstValue("userid");
            return int.TryParse(value, out int id) ? id : -1;
        }
    }

    public Guid UserGuid
    {
        get
        {
            var value = _principal?.FindFirstValue("userguid");
            return Guid.TryParse(value, out Guid g) ? g : Guid.Empty;
        }
    }

    public bool IsInRole(string role) => _principal?.IsInRole(role) == true;
}
