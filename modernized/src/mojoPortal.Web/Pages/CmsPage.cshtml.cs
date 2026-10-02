using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using mojoPortal.Business;
using mojoPortal.Business.Authorization;
using mojoPortal.Web.Services;
using mojoPortal.Web.ViewModels;

namespace mojoPortal.Web.Pages;

/// <summary>
/// Host page for the friendly-URL route (MapFallbackToPage("/CmsPage")).
/// Port of Default.aspx.cs: resolves the CMS page, applies the page-level
/// view gate, then renders each visible module in its pane with the
/// per-module visibility rules.
/// </summary>
public class CmsPageModel : PageModel
{
    private readonly MojoContext _ctx;
    private readonly PermissionService _perms;
    private readonly IMojoUser _user;
    private readonly ItemListBuilder _listBuilder;

    public CmsPageModel(MojoContext ctx, PermissionService perms, IMojoUser user,
        ItemListBuilder listBuilder)
    {
        _ctx = ctx;
        _perms = perms;
        _user = user;
        _listBuilder = listBuilder;
    }

    public SitePage? CurrentPage { get; private set; }
    public Site? CurrentSite { get; private set; }
    public string SiteRoot { get; private set; } = string.Empty;
    public List<Module> LeftPaneModules { get; } = new();
    public List<Module> ContentPaneModules { get; } = new();
    public List<Module> RightPaneModules { get; } = new();
    public Dictionary<int, ItemListModel> ItemLists { get; } = new();
    public Dictionary<int, ListConfiguration> Configs { get; } = new();
    public Dictionary<int, bool> CanEdit { get; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        var path = Request.Path.Value ?? "/";
        CurrentPage = await _ctx.ResolvePageAsync(path, ct);
        CurrentSite = await _ctx.GetSiteAsync(ct);

        if (CurrentPage is null)
        {
            return NotFound();
        }

        if (!_perms.UserCanViewPage(_user, CurrentPage))
        {
            return Redirect("/AccessDenied.aspx");
        }

        // ResolvePageAsync already loaded modules through the publish window.

        bool isAdmin = WebUser.IsAdmin(_user);
        bool isContentAdmin = WebUser.IsContentAdmin(_user);
        bool isSiteEditor = _perms.UserIsSiteEditor(_user);

        foreach (var m in CurrentPage.Modules)
        {
            // Default.aspx.cs visibility: HideFrom* flags, ViewRoles,
            // Admins-only role shortcut.
            if (m.HideFromAuthenticated && _user.IsAuthenticated) { continue; }
            if (m.HideFromUnauthenticated && !_user.IsAuthenticated) { continue; }
            if (m.ViewRoles == "Admins;" && !isAdmin) { continue; }
            if (!WebUser.IsInRoles(_user, m.ViewRoles) && !isContentAdmin && !isSiteEditor) { continue; }

            switch (m.PaneName)
            {
                case "leftpane": LeftPaneModules.Add(m); break;
                case "rightpane": RightPaneModules.Add(m); break;
                default: ContentPaneModules.Add(m); break;
            }
        }

        var allVisible = LeftPaneModules.Concat(ContentPaneModules).Concat(RightPaneModules);
        foreach (var m in allVisible)
        {
            if (m.FeatureGuid != Link.FeatureGuid) { continue; }

            var settings = await _ctx.GetModuleSettingsAsync(m.ModuleId, ct);
            var config = new ListConfiguration(settings);
            Configs[m.ModuleId] = config;

            bool canEdit = _perms.UserCanEditModule(_user, CurrentPage, m.ModuleId, Link.FeatureGuid);
            CanEdit[m.ModuleId] = canEdit;

            // ItemList renders page 1 on initial page load; paging links
            // target ViewList.aspx (non-ajax) — ajax UpdatePanel postback
            // paging is documented as a deviation in SCOPE.md.
            ItemLists[m.ModuleId] = await _listBuilder.BuildAsync(
                m.ModuleId, m.PageId, config, canEdit, pageNumber: 1,
                siteRoot: SiteRoot, imageSiteRoot: SiteRoot, ct);
        }

        if (CurrentSite is not null)
        {
            var topicTitle = CurrentPage.PageTitle.Length > 0 ? CurrentPage.PageTitle : CurrentPage.PageName;
            ViewData["Title"] = $"{CurrentSite.SiteName} - {topicTitle}";
        }
        ViewData["BodyClass"] = "cmspage";

        return Page();
    }
}
