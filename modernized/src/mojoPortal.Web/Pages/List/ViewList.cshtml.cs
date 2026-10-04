using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using mojoPortal.Business;
using mojoPortal.Business.Authorization;
using mojoPortal.Web.Services;
using mojoPortal.Web.ViewModels;

namespace mojoPortal.Web.Pages.List;

/// <summary>
/// Port of ViewList.aspx.cs: standalone paged view of a Links module.
/// </summary>
public class ViewListModel : PageModel
{
    private readonly MojoContext _ctx;
    private readonly PermissionService _perms;
    private readonly IMojoUser _user;
    private readonly ItemListBuilder _listBuilder;

    public ViewListModel(MojoContext ctx, PermissionService perms, IMojoUser user, ItemListBuilder listBuilder)
    {
        _ctx = ctx;
        _perms = perms;
        _user = user;
        _listBuilder = listBuilder;
    }

    public SitePage? CurrentPage { get; private set; }
    public Module? Module { get; private set; }
    public ItemListModel? ItemList { get; private set; }
    public bool CanEdit { get; private set; }
    public string? CanonicalUrl { get; private set; }
    public string? NextUrl { get; private set; }
    public string? PrevUrl { get; private set; }
    public int PageId { get; private set; } = -1;
    public int ModuleId { get; private set; } = -1;
    public string SiteRoot { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync([FromQuery(Name = "pageid")] int pageId = -1,
        [FromQuery(Name = "mid")] int moduleId = -1,
        [FromQuery(Name = "pagenumber")] int pageNumber = 1,
        CancellationToken ct = default)
    {
        PageId = pageId;
        ModuleId = moduleId;

        CurrentPage = await _ctx.GetPageByIdAsync(pageId, ct);

        if (!_perms.UserCanViewPage(_user, CurrentPage, moduleId, Link.FeatureGuid))
        {
            return Redirect("/AccessDenied.aspx");
        }

        var site = await _ctx.GetSiteAsync(ct);
        Module = CurrentPage!.Modules.FirstOrDefault(m => m.ModuleId == moduleId && m.FeatureGuid == Link.FeatureGuid);

        var settings = await _ctx.GetModuleSettingsAsync(moduleId, ct);
        var config = new ListConfiguration(settings);

        CanEdit = _perms.UserCanEditModule(_user, CurrentPage, moduleId, Link.FeatureGuid);

        ItemList = await _listBuilder.BuildAsync(
            moduleId, pageId, config, CanEdit, pageNumber, siteRoot: SiteRoot, imageSiteRoot: SiteRoot, ct);

        // Head links only emitted on the standalone ViewList page in non-ajax
        // paging mode (source condition: Page is ViewListPage inside the
        // !config.UseAjaxPaging branch).
        if (!config.UseAjaxPaging)
        {
            string baseUrl = $"{SiteRoot}/List/ViewList.aspx?pageid={pageId}&mid={moduleId}";
            CanonicalUrl = $"{baseUrl}&pagenumber={pageNumber}";
            NextUrl = $"{baseUrl}&pagenumber={pageNumber + 1}";
            PrevUrl = $"{baseUrl}&pagenumber={pageNumber - 1}";
        }

        if (site is not null)
        {
            ViewData["Title"] = $"{site.SiteName} - {CurrentPage.PageName}";
        }
        ViewData["BodyClass"] = "listviewlist";

        return Page();
    }
}
