namespace mojoPortal.Business;

/// <summary>
/// Domain service for the Links feature. Save/Delete semantics ported from
/// mojoPortal.Features.Business LinkModule/Links.cs: a Link with ItemID == -1
/// is created (new ItemGuid assigned), otherwise updated. The ContentChanged
/// indexing hook is preserved as a no-op event seam (search indexing is out of
/// the slice; see docs/SCOPE.md).
/// </summary>
public sealed class LinkService
{
    private readonly ILinkRepository _links;
    private readonly IModuleRepository _modules;

    public event EventHandler<ContentChangedEventArgs>? ContentChanged;

    public LinkService(ILinkRepository links, IModuleRepository modules)
    {
        _links = links;
        _modules = modules;
    }

    public Task<Link?> GetLinkAsync(int itemId, CancellationToken ct = default)
        => _links.GetLinkAsync(itemId, ct);

    public Task<List<Link>> GetLinksAsync(int moduleId, CancellationToken ct = default)
        => _links.GetLinksAsync(moduleId, ct);

    public Task<(List<Link> Items, int TotalPages)> GetPageAsync(
        int moduleId, int pageNumber, int pageSize, CancellationToken ct = default)
        => _links.GetPageAsync(moduleId, pageNumber, pageSize, ct);

    public async Task<bool> SaveAsync(Link link, CancellationToken ct = default)
    {
        bool result;
        if (link.ItemId > -1)
        {
            result = await _links.UpdateLinkAsync(link, ct);
        }
        else
        {
            // Source: Create() assigns ItemGuid = Guid.NewGuid() before insert.
            link.ItemGuid = Guid.NewGuid();
            link.ItemId = await _links.AddLinkAsync(link, ct);
            result = link.ItemId > 0;
        }

        if (result)
        {
            OnContentChanged(new ContentChangedEventArgs());
        }

        return result;
    }

    public async Task<bool> DeleteAsync(int itemId, CancellationToken ct = default)
    {
        bool result = await _links.DeleteLinkAsync(itemId, ct);
        if (result)
        {
            OnContentChanged(new ContentChangedEventArgs { IsDeleted = true });
        }

        return result;
    }

    public Task<bool> DeleteByModuleAsync(int moduleId, CancellationToken ct = default)
        => _links.DeleteByModuleAsync(moduleId, ct);

    public Task<bool> DeleteBySiteAsync(int siteId, CancellationToken ct = default)
        => _links.DeleteBySiteAsync(siteId, ct);

    /// <summary>
    /// FK_Links_Modules ON DELETE CASCADE equivalent: when a module is deleted,
    /// its links go with it.
    /// </summary>
    public async Task<bool> DeleteModuleCascadeAsync(int moduleId, CancellationToken ct = default)
    {
        await _links.DeleteByModuleAsync(moduleId, ct);
        return await _modules.DeleteAsync(moduleId, ct);
    }

    private void OnContentChanged(ContentChangedEventArgs e)
        => ContentChanged?.Invoke(this, e);
}

public sealed class ContentChangedEventArgs : EventArgs
{
    public bool IsDeleted { get; set; }
}
