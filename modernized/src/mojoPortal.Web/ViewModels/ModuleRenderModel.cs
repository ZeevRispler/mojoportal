namespace mojoPortal.Web.ViewModels;

/// <summary>Everything a pane needs to render one module instance.</summary>
public sealed class ModuleRenderModel
{
    public required Business.Module Module { get; init; }
    public required bool CanEdit { get; init; }
    public required string SiteRoot { get; init; }
    public ItemListModel? ItemList { get; init; }
}
