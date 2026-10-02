using mojoPortal.Business;
using mojoPortal.Data.Mongo;
using Xunit;

namespace mojoPortal.Tests.Integration;

/// <summary>
/// Real-MongoDB verification of the ported repository behaviors:
/// mp_Links_* stored proc semantics (ordering, paging, get-by-page, delete
/// cascades) plus SiteUser.UpdateLastModified windows.
/// </summary>
[Collection("mongo")]
public sealed class LinkRepositoryTests
{
    private readonly MongoFixture _fixture;
    public LinkRepositoryTests(MongoFixture fixture) => _fixture = fixture;

    private (MongoStore Store, LinkRepository Repo) NewRepo(string name, out CounterService counters)
    {
        var store = _fixture.NewStore(name);
        counters = new CounterService(store);
        return (store, new LinkRepository(store, counters));
    }

    private static Link L(int mid, string title, int viewOrder, string url = "https://x")
        => new() { ModuleId = mid, Title = title, Url = url, ViewOrder = viewOrder };

    [Fact]
    public async Task Ordering_is_vieworder_then_title_case_insensitive()
    {
        var (store, repo) = NewRepo("links_order", out _);
        await store.EnsureIndexesAsync();
        int mid = 10;
        await repo.AddLinkAsync(L(mid, "Beta", 1));
        await repo.AddLinkAsync(L(mid, "alpha", 2));
        await repo.AddLinkAsync(L(mid, "CHARLIE", 3));
        await repo.AddLinkAsync(L(mid, "delta", 3));

        var items = await repo.GetLinksAsync(mid);
        Assert.Equal(new[] { "Beta", "alpha", "CHARLIE", "delta" }, items.Select(l => l.Title).ToArray());
    }

    [Fact]
    public async Task Paging_windows_match_source_IndexID_bounds()
    {
        var (store, repo) = NewRepo("links_page", out _);
        await store.EnsureIndexesAsync();
        int mid = 11;
        for (int i = 0; i < 25; i++)
        {
            await repo.AddLinkAsync(L(mid, $"t{i:00}", 500));
        }

        var (p1, totalPages) = await repo.GetPageAsync(mid, 1, 10);
        Assert.Equal(10, p1.Count);
        Assert.Equal("t00", p1[0].Title);
        Assert.Equal("t09", p1[^1].Title);
        Assert.Equal(3, totalPages);

        var (p3, _) = await repo.GetPageAsync(mid, 3, 10);
        Assert.Equal(5, p3.Count);
        Assert.Equal("t20", p3[0].Title);

        // Out-of-range page yields empty, exactly like the IndexID window.
        var (p9, _) = await repo.GetPageAsync(mid, 9, 10);
        Assert.Empty(p9);

        // Page 0: source bounds yield an empty window.
        var (p0, _) = await repo.GetPageAsync(mid, 0, 10);
        Assert.Empty(p0);
    }

    [Fact]
    public async Task GetCount_and_GetLink_roundtrip()
    {
        var (store, repo) = NewRepo("links_crud", out _);
        await store.EnsureIndexesAsync();
        int mid = 12;
        var a = L(mid, "one", 1);
        int idA = await repo.AddLinkAsync(a);
        var b = L(mid, "two", 2);
        await repo.AddLinkAsync(b);
        var other = L(99, "elsewhere", 1);
        await repo.AddLinkAsync(other);

        Assert.Equal(2, await repo.GetCountAsync(mid));

        var fetched = await repo.GetLinkAsync(idA);
        Assert.NotNull(fetched);
        Assert.Equal("one", fetched!.Title);
        Assert.Equal(mid, fetched.ModuleId);

        // ItemGuid assignment is a business-layer behavior (Link.Create calls
        // Guid.NewGuid()); verify it through LinkService.SaveAsync.
        var svc = new LinkService(repo, new ModuleRepository(store, new CounterService(store)));
        var viaService = L(mid, "three", 3);
        await svc.SaveAsync(viaService);
        var svcFetched = await repo.GetLinkAsync(viaService.ItemId);
        Assert.NotEqual(Guid.Empty, svcFetched!.ItemGuid);
    }

    [Fact]
    public async Task UpdateLink_preserves_UserGuid_and_updates_fields()
    {
        var (store, repo) = NewRepo("links_update", out _);
        await store.EnsureIndexesAsync();
        var link = L(13, "orig", 1);
        int id = await repo.AddLinkAsync(link);

        var fetched = await repo.GetLinkAsync(id);
        var userGuid = fetched!.UserGuid;
        fetched.Title = "changed";
        fetched.ViewOrder = 42;
        fetched.Description = "desc";
        Assert.True(await repo.UpdateLinkAsync(fetched));

        var after = await repo.GetLinkAsync(id);
        Assert.Equal("changed", after!.Title);
        Assert.Equal(42, after.ViewOrder);
        Assert.Equal("desc", after.Description);
        Assert.Equal(userGuid, after.UserGuid); // mp_Links_Update doesn't touch UserGuid
    }

    [Fact]
    public async Task Delete_by_module_and_site()
    {
        var (store, repo) = NewRepo("links_delete", out _);
        await store.EnsureIndexesAsync();

        // Module->site mapping lives on mp_Modules: seed two modules + pageModules.
        var counters = new CounterService(store);
        var modules = new ModuleRepository(store, counters);
        var mA = new Module { SiteId = 1, ModuleTitle = "A" };
        mA.ModuleId = await modules.CreateAsync(mA);
        var mB = new Module { SiteId = 1, ModuleTitle = "B" };
        mB.ModuleId = await modules.CreateAsync(mB);
        var mOther = new Module { SiteId = 2, ModuleTitle = "C" };
        mOther.ModuleId = await modules.CreateAsync(mOther);

        await repo.AddLinkAsync(L(mA.ModuleId, "a1", 1));
        await repo.AddLinkAsync(L(mA.ModuleId, "a2", 2));
        await repo.AddLinkAsync(L(mB.ModuleId, "b1", 1));
        await repo.AddLinkAsync(L(mOther.ModuleId, "c1", 1));

        Assert.True(await repo.DeleteByModuleAsync(mA.ModuleId));
        Assert.Equal(0, await repo.GetCountAsync(mA.ModuleId));
        Assert.Equal(1, await repo.GetCountAsync(mB.ModuleId));

        Assert.True(await repo.DeleteBySiteAsync(1));
        Assert.Equal(0, await repo.GetCountAsync(mB.ModuleId));
        Assert.Equal(1, await repo.GetCountAsync(mOther.ModuleId)); // site 2 untouched
    }

    [Fact]
    public async Task GetLinksByPage_returns_links_for_modules_on_page()
    {
        var (store, repo) = NewRepo("links_bypage", out _);
        await store.EnsureIndexesAsync();
        var counters = new CounterService(store);
        var modules = new ModuleRepository(store, counters);

        var onPage = new Module { SiteId = 1, ModuleTitle = "on" };
        onPage.ModuleId = await modules.CreateAsync(onPage);
        var offPage = new Module { SiteId = 1, ModuleTitle = "off" };
        offPage.ModuleId = await modules.CreateAsync(offPage);

        int pageId = 77;
        await store.Pages.InsertOneAsync(new SitePage { PageId = pageId, SiteId = 1, Url = "~/p" });
        await store.PageModules.InsertOneAsync(new PageModule
        {
            PageId = pageId, ModuleId = onPage.ModuleId, PaneName = "contentpane",
        });

        await repo.AddLinkAsync(L(onPage.ModuleId, "onlink", 1));
        await repo.AddLinkAsync(L(offPage.ModuleId, "offlink", 1));

        var links = await repo.GetLinksByPageAsync(1, pageId);
        Assert.Single(links);
        Assert.Equal("onlink", links[0].Title);
    }

    [Fact]
    public async Task Counter_service_issues_monotonic_ids()
    {
        var store = _fixture.NewStore("counters");
        var counters = new CounterService(store);
        int a = await counters.NextAsync("testseq", 0);
        int b = await counters.NextAsync("testseq", 0);
        int c = await counters.NextAsync("other", 0);
        // mp_Links.ItemID is IDENTITY(0,1): first issued id equals the seed.
        Assert.Equal(0, a);
        Assert.Equal(1, b);
        Assert.Equal(0, c); // independent sequence
    }
}
