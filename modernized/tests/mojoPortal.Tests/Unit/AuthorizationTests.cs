using mojoPortal.Business;
using mojoPortal.Business.Authorization;
using Xunit;

namespace mojoPortal.Tests.Unit;

/// <summary>
/// Source-derived assertions for WebUser role semantics and the
/// UserCanViewPage/UserCanEditModule decision order — including the second
/// loop's missing feature-guid re-check quirk.
/// </summary>
public sealed class AuthorizationTests
{
    private sealed class FakeUser : IMojoUser
    {
        private readonly HashSet<string> _roles;
        public FakeUser(bool authenticated, int userId = 1, params string[] roles)
        {
            IsAuthenticated = authenticated;
            UserId = userId;
            _roles = new HashSet<string>(roles);
        }

        public bool IsAuthenticated { get; }
        public int UserId { get; }
        public Guid UserGuid { get; } = Guid.NewGuid();
        public bool IsInRole(string role) => _roles.Contains(role);
    }

    private static SitePage PageWithModule(Module m, string authorizedRoles = "All Users;", string editRoles = "")
        => new()
        {
            PageId = 5,
            AuthorizedRoles = authorizedRoles,
            EditRoles = editRoles,
            Modules = { m },
        };

    private static Module LinksModule(int id, string viewRoles = "All Users;", string editRoles = "", int editUserId = -1)
        => new()
        {
            ModuleId = id,
            FeatureGuid = Link.FeatureGuid,
            ViewRoles = viewRoles,
            AuthorizedEditRoles = editRoles,
            EditUserId = editUserId,
        };

    [Fact]
    public void All_Users_role_matches_anonymous()
        => Assert.True(WebUser.IsInRole(new FakeUser(false), "All Users"));

    [Fact]
    public void Non_all_users_requires_auth()
        => Assert.False(WebUser.IsInRole(new FakeUser(false), "Editors"));

    [Fact]
    public void Admins_member_passes_every_check()
    {
        var admin = new FakeUser(true, roles: "Admins");
        Assert.True(WebUser.IsInRole(admin, "Editors"));
        Assert.True(WebUser.IsInRoles(admin, "Editors;"));
    }

    [Fact]
    public void Anonymous_never_admin_or_contentadmin()
    {
        var anon = new FakeUser(false);
        Assert.False(WebUser.IsAdmin(anon));
        Assert.False(WebUser.IsContentAdmin(anon));
    }

    [Fact]
    public void IsInRoles_all_users_string_matches()
        => Assert.True(WebUser.IsInRoles(new FakeUser(false), "All Users;"));

    [Fact]
    public void Page_view_gate_source_matrix()
    {
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(1), authorizedRoles: "Editors;");
        var editor = new FakeUser(true, roles: "Editors");
        var anon = new FakeUser(false);
        var admin = new FakeUser(true, roles: "Admins");
        var cadmin = new FakeUser(true, roles: "Content Administrators");

        Assert.True(svc.UserCanViewPage(editor, page));
        Assert.False(svc.UserCanViewPage(anon, page));
        Assert.True(svc.UserCanViewPage(admin, page));
        Assert.True(svc.UserCanViewPage(cadmin, page)); // AuthorizedRoles != "Admins;"

        page.AuthorizedRoles = "Admins;";
        Assert.False(svc.UserCanViewPage(cadmin, page));
        Assert.True(svc.UserCanViewPage(admin, page));
    }

    [Fact]
    public void Module_view_requires_module_on_page_and_roles()
    {
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7));
        var anon = new FakeUser(false);

        Assert.True(svc.UserCanViewPage(anon, page, 7, Link.FeatureGuid));
        Assert.False(svc.UserCanViewPage(anon, page, 8, Link.FeatureGuid));
        Assert.False(svc.UserCanViewPage(anon, page, 7, Guid.NewGuid())); // wrong feature guid
    }

    [Fact]
    public void Edit_module_anonymous_and_null_fail()
    {
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7));
        Assert.False(svc.UserCanEditModule(null, page, 7, Link.FeatureGuid));
        Assert.False(svc.UserCanEditModule(new FakeUser(false), page, 7, Link.FeatureGuid));
    }

    [Fact]
    public void Edit_module_admin_always_passes()
    {
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7, editRoles: "Admins;"));
        Assert.True(svc.UserCanEditModule(new FakeUser(true, roles: "Admins"), page, 7, Link.FeatureGuid));
    }

    [Fact]
    public void Content_admin_passes_when_page_editroles_not_admins()
    {
        var svc = new PermissionService();
        // Module not even on the page: source passes anyway ("to support modulewrapper").
        var page = PageWithModule(LinksModule(7), editRoles: "");
        var cadmin = new FakeUser(true, roles: "Content Administrators");
        Assert.True(svc.UserCanEditModule(cadmin, page, 999, Link.FeatureGuid));
    }

    [Fact]
    public void Content_admin_blocked_when_module_editroles_admins()
    {
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7, editRoles: "Admins;"), editRoles: "");
        var cadmin = new FakeUser(true, roles: "Content Administrators");
        Assert.False(svc.UserCanEditModule(cadmin, page, 7, Link.FeatureGuid));
    }

    [Fact]
    public void Content_admin_blocked_when_page_editroles_admins_and_module_not_found()
    {
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7), editRoles: "Admins;");
        var cadmin = new FakeUser(true, roles: "Content Administrators");
        Assert.False(svc.UserCanEditModule(cadmin, page, 7, Link.FeatureGuid));
    }

    [Fact]
    public void EdituserId_match_grants_edit()
    {
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7, editUserId: 42), editRoles: "");
        var user = new FakeUser(true, userId: 42);
        Assert.True(svc.UserCanEditModule(user, page, 7, Link.FeatureGuid));
    }

    [Fact]
    public void Authorized_edit_roles_grant_edit()
    {
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7, editRoles: "LinkEditors;"), editRoles: "");
        var user = new FakeUser(true, roles: "LinkEditors");
        Assert.True(svc.UserCanEditModule(user, page, 7, Link.FeatureGuid));
    }

    [Fact]
    public void Plain_user_cannot_edit()
    {
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7, editRoles: "LinkEditors;"), editRoles: "");
        var user = new FakeUser(true, roles: "Members");
        Assert.False(svc.UserCanEditModule(user, page, 7, Link.FeatureGuid));
    }

    [Fact]
    public void Page_editroles_grant_edit_for_non_global_module()
    {
        var svc = new PermissionService();
        var m = LinksModule(7);
        var page = PageWithModule(m, editRoles: "PageEditors;");
        var user = new FakeUser(true, roles: "PageEditors");
        Assert.True(svc.UserCanEditModule(user, page, 7, Link.FeatureGuid));
    }

    [Fact]
    public void Wrong_feature_guid_blocked_by_found_on_page_gate()
    {
        // Source quirk interplay: the second scan skips the guid re-check, but
        // the !moduleFoundOnPage gate above it still rejects a wrong feature
        // guid before that loop ever runs.
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7, editRoles: "LinkEditors;"), editRoles: "");
        var user = new FakeUser(true, roles: "LinkEditors");
        Assert.False(svc.UserCanEditModule(user, page, 7, Guid.NewGuid()));
    }

    [Fact]
    public void Second_loop_ignores_guid_when_feature_guid_empty()
    {
        // featureGuid == Guid.Empty skips the loop-1 guid check entirely, so
        // the second loop's role match stands on ModuleId alone.
        var svc = new PermissionService();
        var page = PageWithModule(LinksModule(7, editRoles: "LinkEditors;"), editRoles: "");
        var user = new FakeUser(true, roles: "LinkEditors");
        Assert.True(svc.UserCanEditModule(user, page, 7, Guid.Empty));
    }

    [Fact]
    public void Global_module_skips_page_editroles()
    {
        var svc = new PermissionService();
        var m = LinksModule(7);
        m.IsGlobal = true;
        var page = PageWithModule(m, editRoles: "PageEditors;");
        var user = new FakeUser(true, roles: "PageEditors");
        Assert.False(svc.UserCanEditModule(user, page, 7, Link.FeatureGuid));
    }
}
