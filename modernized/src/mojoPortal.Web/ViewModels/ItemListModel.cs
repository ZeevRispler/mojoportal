using mojoPortal.Business;

namespace mojoPortal.Web.ViewModels;

/// <summary>
/// View model for the shared Links item list (port of ItemList.ascx state).
/// </summary>
public sealed class ItemListModel
{
    public required ListConfiguration Config { get; init; }
    public int ModuleId { get; init; }
    public int PageId { get; init; }
    public bool IsEditable { get; init; }
    public string SiteRoot { get; init; } = string.Empty;
    public string ImageSiteRoot { get; init; } = string.Empty;
    public IReadOnlyList<Link> Items { get; init; } = Array.Empty<Link>();

    // pager state (EnablePager path only)
    public int PageNumber { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public bool ShowPager { get; init; }
    public bool DescriptionAboveLink { get; init; } // displaySettings.DescriptionAboveLink, default false

    public string FormatEditUrl(int itemId)
        => SiteRoot + "/List/Edit.aspx?ItemID=" + itemId + "&mid=" + ModuleId + "&pageid=" + PageId;

    public string FormatPageUrl(int pageNumber)
        => SiteRoot + "/List/ViewList.aspx?pageid=" + PageId + "&mid=" + ModuleId + "&pagenumber=" + pageNumber;
}
