namespace mojoPortal.Business.Authorization;

/// <summary>
/// Ambient user abstraction standing in for HttpContext.Current.User.
/// </summary>
public interface IMojoUser
{
    bool IsAuthenticated { get; }
    int UserId { get; }
    Guid UserGuid { get; }
    bool IsInRole(string role);
}

/// <summary>
/// Port of mojoPortal.Business.WebHelpers WebUser role semantics.
/// "All Users" matches everyone including anonymous; an "Admins" member passes
/// every role check; every other check requires authentication.
/// </summary>
public static class WebUser
{
    public static bool IsInRole(IMojoUser? user, string role)
    {
        if (user is null || string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        if (role.Contains("All Users"))
        {
            return true;
        }

        if (!user.IsAuthenticated)
        {
            return false;
        }

        if (user.IsInRole("Admins"))
        {
            return true;
        }

        return user.IsInRole(role);
    }

    public static bool IsInRoles(IMojoUser? user, string? roles)
    {
        if (IsInRole(user, "Admins"))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(roles))
        {
            return false;
        }

        if (roles.Contains("All Users;"))
        {
            return true;
        }

        if (user is null || !user.IsAuthenticated)
        {
            return false;
        }

        foreach (string role in roles.Split(';'))
        {
            if (role.IndexOf("All Users", StringComparison.Ordinal) > -1)
            {
                return true;
            }

            if (IsInRole(user, role))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsAdmin(IMojoUser? user)
    {
        if (user is null || !user.IsAuthenticated)
        {
            return false;
        }

        return IsInRole(user, "Admins");
    }

    public static bool IsContentAdmin(IMojoUser? user)
    {
        if (user is null || !user.IsAuthenticated)
        {
            return false;
        }

        return IsInRole(user, "Content Administrators");
    }

    public static bool IsAdminOrContentAdmin(IMojoUser? user) => IsAdmin(user) || IsContentAdmin(user);
}
