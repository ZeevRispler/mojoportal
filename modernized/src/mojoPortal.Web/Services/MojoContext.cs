using mojoPortal.Business;

namespace mojoPortal.Web.Services;

/// <summary>
/// Per-request CMS context: current site, current page (with its modules
/// filtered by the publish window, like mp_Modules_SelectByPage), and module
/// settings. Mirrors CacheHelper.GetCurrentSiteSettings/GetCurrentPage.
/// </summary>
public sealed class MojoContext
{
    private readonly ISiteRepository _sites;
    private readonly IPageRepository _pages;
    private readonly IModuleRepository _modules;
    private readonly IModuleSettingsRepository _settings;

    public MojoContext(
        ISiteRepository sites,
        IPageRepository pages,
        IModuleRepository modules,
        IModuleSettingsRepository settings)
    {
        _sites = sites;
        _pages = pages;
        _modules = modules;
        _settings = settings;
    }

    public Site? Site { get; private set; }
    public SitePage? CurrentPage { get; private set; }

    public async Task<Site?> GetSiteAsync(CancellationToken ct = default)
    {
        Site ??= await _sites.GetFirstSiteAsync(ct);
        return Site;
    }

    public async Task<SitePage?> GetPageByIdAsync(int pageId, CancellationToken ct = default)
    {
        if (CurrentPage is not null && CurrentPage.PageId == pageId)
        {
            return CurrentPage;
        }

        var page = await _pages.GetByIdAsync(pageId, ct);
        if (page is not null)
        {
            await LoadModulesAsync(page, ct);
        }
        CurrentPage = page;
        return page;
    }

    /// <summary>
    /// Legacy URL rewriting: a request path resolves through mp_FriendlyUrls
    /// first (friendly name -> real ~/Default.aspx?pageid=N), then mp_Pages.Url
    /// (stored as "~/name"). An empty path resolves the default (home) page.
    /// </summary>
    public async Task<SitePage?> ResolvePageAsync(string requestPath, CancellationToken ct = default)
    {
        var site = await GetSiteAsync(ct);
        if (site is null) { return null; }

        string path = requestPath.Trim('/');
        SitePage? page = null;

        if (path.Length == 0)
        {
            page = await _pages.GetDefaultPageAsync(site.SiteId, ct);
        }
        else
        {
            int? pageId = await _pages.ResolvePageIdByFriendlyUrlAsync(site.SiteId, path, ct);
            if (pageId.HasValue)
            {
                page = await _pages.GetByIdAsync(pageId.Value, ct);
            }

            page ??= await _pages.GetByUrlAsync(site.SiteId, "~/" + path, ct);
        }

        if (page is not null)
        {
            await LoadModulesAsync(page, ct);
        }

        CurrentPage = page;
        return page;
    }

    private async Task LoadModulesAsync(SitePage page, CancellationToken ct)
    {
        // mp_Modules_SelectByPage uses DateTime.UtcNow for @CurrentTime.
        page.Modules = (await _modules.GetPageModulesAsync(page.PageId, DateTime.UtcNow, ct))
            .Cast<Module>()
            .ToList();
    }

    public Task<Dictionary<string, string>> GetModuleSettingsAsync(int moduleId, CancellationToken ct = default)
        => _settings.GetModuleSettingsAsync(moduleId, ct);

    /// <summary>The current page's friendly URL, mirroring GetCurrentPageUrl.</summary>
    public static string GetCurrentPageUrl(SitePage? page)
    {
        if (page is null || page.Url.Length == 0) { return "/"; }
        return page.Url.StartsWith("~/") ? page.Url[1..] : page.Url;
    }
}
