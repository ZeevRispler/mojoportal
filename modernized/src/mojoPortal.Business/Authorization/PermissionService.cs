namespace mojoPortal.Business.Authorization;

/// <summary>
/// Port of mojoBasePage.UserCanViewPage / UserCanEditModule and
/// SiteUtils.UserIsSiteEditor. Decision order and quirks are preserved
/// verbatim, including the second scan in UserCanEditModule that matches
/// ModuleId without re-checking the feature guid.
/// </summary>
public sealed class PermissionService
{
    private readonly bool _relatedSitesEnabled;
    private readonly string _siteRootEditRoles;

    public PermissionService(bool relatedSitesEnabled = false, string siteRootEditRoles = "")
    {
        _relatedSitesEnabled = relatedSitesEnabled;
        _siteRootEditRoles = siteRootEditRoles;
    }

    /// <summary>SiteUtils.UserIsSiteEditor: admin always, else only in related-sites mode.</summary>
    public bool UserIsSiteEditor(IMojoUser? user)
    {
        if (WebUser.IsAdmin(user)) { return true; }

        return _relatedSitesEnabled && WebUser.IsInRoles(user, _siteRootEditRoles);
    }

    /// <summary>Port of mojoBasePage.UserCanViewPage() — the whole-page gate.</summary>
    public bool UserCanViewPage(IMojoUser? user, SitePage? currentPage)
    {
        if (currentPage is null)
        {
            return false;
        }

        if (WebUser.IsAdmin(user))
        {
            return true;
        }

        if (WebUser.IsContentAdmin(user))
        {
            return currentPage.AuthorizedRoles != "Admins;";
        }

        if (UserIsSiteEditor(user))
        {
            return currentPage.AuthorizedRoles != "Admins;";
        }

        if (WebUser.IsInRoles(user, currentPage.AuthorizedRoles))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Source: the module must be placed on the current page (with matching
    /// feature guid when given) AND pass the role gates.
    /// </summary>
    public bool UserCanViewPage(IMojoUser? user, SitePage? currentPage, int moduleId, Guid featureGuid)
    {
        if (currentPage is null)
        {
            return false;
        }

        Module? m = GetModule(currentPage, moduleId, featureGuid);
        if (m is null)
        {
            return false;
        }

        if (WebUser.IsAdmin(user))
        {
            return true;
        }

        if (WebUser.IsContentAdmin(user))
        {
            if (currentPage.AuthorizedRoles == "Admins;")
            {
                return false;
            }

            if (m.ViewRoles == "Admins;")
            {
                return false;
            }

            return true;
        }

        if (UserIsSiteEditor(user))
        {
            if (currentPage.AuthorizedRoles == "Admins;")
            {
                return false;
            }

            if (m.ViewRoles == "Admins;")
            {
                return false;
            }

            return true;
        }

        if (!WebUser.IsInRoles(user, m.ViewRoles))
        {
            return false;
        }

        if (WebUser.IsInRoles(user, currentPage.AuthorizedRoles))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Port of mojoBasePage.UserCanEditModule(moduleId, featureGuid).
    /// </summary>
    public bool UserCanEditModule(IMojoUser? user, SitePage? currentPage, int moduleId, Guid featureGuid)
    {
        if (user is null || !user.IsAuthenticated)
        {
            return false;
        }

        if (currentPage is null)
        {
            return false;
        }

        if (WebUser.IsAdmin(user))
        {
            return true;
        }

        bool isSiteEditor = UserIsSiteEditor(user);

        bool moduleFoundOnPage = false;
        bool moduleIsGlobal = false; // global modules can only be edited by admins, content admins or users in module edit roles

        foreach (Module m in currentPage.Modules)
        {
            if (m.ModuleId == moduleId && (featureGuid == Guid.Empty || m.FeatureGuid == featureGuid))
            {
                moduleFoundOnPage = true;
                moduleIsGlobal = m.IsGlobal;

                if (WebUser.IsContentAdmin(user) && m.AuthorizedEditRoles == "Admins;")
                {
                    return false;
                }

                if (isSiteEditor && m.AuthorizedEditRoles == "Admins;")
                {
                    return false;
                }
            }
        }

        if (WebUser.IsContentAdmin(user) && currentPage.EditRoles != "Admins;")
        {
            return true;
        }

        if (isSiteEditor && currentPage.EditRoles != "Admins;")
        {
            return true;
        }

        // the above are allowed even if not on page to support modulewrapper
        if (!moduleFoundOnPage)
        {
            return false;
        }

        if (!moduleIsGlobal && WebUser.IsInRoles(user, currentPage.EditRoles))
        {
            return true;
        }

        if (user.UserId < 0)
        {
            return false;
        }

        foreach (Module m in currentPage.Modules)
        {
            // Source quirk preserved: this loop does NOT re-check featureGuid.
            if (m.ModuleId == moduleId)
            {
                if (m.EditUserId == user.UserId)
                {
                    return true;
                }

                if (WebUser.IsInRoles(user, m.AuthorizedEditRoles))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Module? GetModule(SitePage page, int moduleId, Guid featureGuid)
    {
        foreach (Module m in page.Modules)
        {
            if (m.ModuleId == moduleId && (featureGuid == Guid.Empty || m.FeatureGuid == featureGuid))
            {
                return m;
            }
        }

        return null;
    }
}
