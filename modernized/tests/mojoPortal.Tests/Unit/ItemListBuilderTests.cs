using mojoPortal.Business;
using mojoPortal.Web.Services;
using Xunit;

namespace mojoPortal.Tests.Unit;

/// <summary>
/// Source-derived assertions for ItemList.PopulateControls: EnablePager
/// switches between paged and full-list modes; pager visible iff
/// totalPages &gt; 1.
/// </summary>
public sealed class ItemListBuilderTests
{
    private sealed class FakeLinks : ILinkRepository
    {
        public List<Link> Items = new();
        public int? RequestedPageNumber;
        public int? RequestedPageSize;

        public Task<int> AddLinkAsync(Link l, CancellationToken ct = default) => Task.FromResult(l.ItemId);
        public Task<bool> UpdateLinkAsync(Link l, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> DeleteLinkAsync(int id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> DeleteByModuleAsync(int mid, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> DeleteBySiteAsync(int sid, CancellationToken ct = default) => Task.FromResult(true);
        public Task<Link?> GetLinkAsync(int id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(l => l.ItemId == id));
        public Task<List<Link>> GetLinksAsync(int mid, CancellationToken ct = default)
            => Task.FromResult(Items.Where(l => l.ModuleId == mid).ToList());
        public Task<int> GetCountAsync(int mid, CancellationToken ct = default)
            => Task.FromResult(Items.Count(l => l.ModuleId == mid));
        public Task<(List<Link>, int)> GetPageAsync(int mid, int pageNumber, int pageSize, CancellationToken ct = default)
        {
            RequestedPageNumber = pageNumber;
            RequestedPageSize = pageSize;
            var (skip, take) = LinkPaging.ComputeWindow(pageNumber, pageSize);
            var page = Items.Skip(skip).Take(take).ToList();
            return Task.FromResult((page, LinkPaging.ComputeTotalPages(Items.Count, pageSize)));
        }
        public Task<List<Link>> GetLinksByPageAsync(int sid, int pid, CancellationToken ct = default)
            => Task.FromResult(Items);
    }

    private sealed class FakeModules : IModuleRepository
    {
        public Task<Module?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<Module?>(null);
        public Task<List<Module>> GetPageModulesAsync(int pid, DateTime now, CancellationToken ct = default)
            => Task.FromResult(new List<Module>());
        public Task<List<Module>> GetModulesForSiteAsync(int sid, Guid g, CancellationToken ct = default)
            => Task.FromResult(new List<Module>());
        public Task<int> CreateAsync(Module m, CancellationToken ct = default) => Task.FromResult(m.ModuleId);
        public Task<bool> DeleteAsync(int id, CancellationToken ct = default) => Task.FromResult(true);
    }

    private static Link L(int id, int mid, string title) => new() { ItemId = id, ModuleId = mid, Title = title };

    [Fact]
    public async Task Pager_disabled_returns_all_items()
    {
        var links = new FakeLinks { Items = { L(1, 3, "a"), L(2, 3, "b"), L(3, 3, "c") } };
        var builder = new ItemListBuilder(new LinkService(links, new FakeModules()));
        var config = new ListConfiguration(new Dictionary<string, string>
        {
            ["LinksEnablePagingSetting"] = "false",
        });

        var model = await builder.BuildAsync(3, 1, config, isEditable: false, pageNumber: 1);
        Assert.Equal(3, model.Items.Count);
        Assert.False(model.ShowPager);
        Assert.Null(links.RequestedPageNumber); // GetPageAsync never called
    }

    [Fact]
    public async Task Pager_enabled_pages_via_GetPage_and_shows_pager_when_multiple_pages()
    {
        var links = new FakeLinks();
        for (int i = 0; i < 12; i++) { links.Items.Add(L(i + 1, 3, $"t{i}")); }
        var builder = new ItemListBuilder(new LinkService(links, new FakeModules()));
        var config = new ListConfiguration(new Dictionary<string, string>
        {
            ["LinksEnablePagingSetting"] = "true",
            ["LinksPageSizeSetting"] = "5",
        });

        var model = await builder.BuildAsync(3, 1, config, isEditable: true, pageNumber: 2);
        Assert.Equal(2, links.RequestedPageNumber);
        Assert.Equal(5, links.RequestedPageSize);
        Assert.Equal(5, model.Items.Count);
        Assert.Equal(3, model.TotalPages);
        Assert.True(model.ShowPager);
    }

    [Fact]
    public async Task Pager_hidden_when_single_page()
    {
        var links = new FakeLinks { Items = { L(1, 3, "a") } };
        var builder = new ItemListBuilder(new LinkService(links, new FakeModules()));
        var config = new ListConfiguration(new Dictionary<string, string>
        {
            ["LinksEnablePagingSetting"] = "true",
            ["LinksPageSizeSetting"] = "5",
        });

        var model = await builder.BuildAsync(3, 1, config, isEditable: false, pageNumber: 1);
        Assert.Equal(1, model.TotalPages);
        Assert.False(model.ShowPager); // pgr.Visible = totalPages > 1
    }
}
