using mojoPortal.Business;
using mojoPortal.Web.ViewModels;

namespace mojoPortal.Web.Services;

/// <summary>
/// Port of ItemList.PopulateControls: EnablePager path pages via GetPage with
/// the module's PageSize; otherwise the full ordered list. Pager is only shown
/// when totalPages &gt; 1.
/// </summary>
public sealed class ItemListBuilder
{
    private readonly LinkService _links;

    public ItemListBuilder(LinkService links) => _links = links;

    public async Task<ItemListModel> BuildAsync(
        int moduleId,
        int pageId,
        ListConfiguration config,
        bool isEditable,
        int pageNumber,
        string siteRoot = "",
        string imageSiteRoot = "",
        CancellationToken ct = default)
    {
        IReadOnlyList<Link> items;
        int totalPages = 1;
        bool showPager = false;

        if (config.EnablePager)
        {
            var page = await _links.GetPageAsync(moduleId, pageNumber, config.PageSize, ct);
            items = page.Items;
            totalPages = page.TotalPages;
            showPager = true; // visibility decided by TotalPages > 1 like the source
        }
        else
        {
            items = await _links.GetLinksAsync(moduleId, ct);
        }

        return new ItemListModel
        {
            Config = config,
            ModuleId = moduleId,
            PageId = pageId,
            IsEditable = isEditable,
            SiteRoot = siteRoot,
            ImageSiteRoot = imageSiteRoot,
            Items = items,
            PageNumber = pageNumber,
            TotalPages = totalPages,
            ShowPager = showPager && totalPages > 1,
        };
    }
}
