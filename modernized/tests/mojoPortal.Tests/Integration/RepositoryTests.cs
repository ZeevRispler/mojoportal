using MongoDB.Driver;
using mojoPortal.Business;
using mojoPortal.Data.Mongo;
using Xunit;

namespace mojoPortal.Tests.Integration;

[Collection("mongo")]
public sealed class RepositoryTests
{
    private readonly MongoFixture _fixture;
    public RepositoryTests(MongoFixture fixture) => _fixture = fixture;

    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    [Fact]
    public async Task Module_settings_upsert_sets_guid_on_insert_updates_only_value()
    {
        var store = _fixture.NewStore("settings");
        await store.EnsureIndexesAsync();
        var counters = new CounterService(store);
        var repo = new ModuleSettingsRepository(store, counters);
        var moduleGuid = Guid.NewGuid();

        await repo.UpdateModuleSettingAsync(moduleGuid, 5, "IntroContent", "<p>v1</p>");
        var doc1 = await repo.GetModuleSettingsAsync(5);
        Assert.Equal("<p>v1</p>", doc1["IntroContent"]);

        var row1 = await store.ModuleSettings.Find(s => s.ModuleId == 5).FirstAsync();
        var guid1 = row1.SettingGuid;
        Assert.NotEqual(Guid.Empty, guid1);
        Assert.Equal(moduleGuid, row1.ModuleGuid);

        await repo.UpdateModuleSettingAsync(moduleGuid, 5, "IntroContent", "<p>v2</p>");
        var row2 = await store.ModuleSettings.Find(s => s.ModuleId == 5).FirstAsync();
        Assert.Equal("<p>v2</p>", row2.SettingValue);
        Assert.Equal(guid1, row2.SettingGuid); // UPDATE keeps the inserted guid
        Assert.Equal(1, await store.ModuleSettings.CountDocumentsAsync(s => s.ModuleId == 5));
    }

    [Fact]
    public async Task Module_settings_null_value_roundtrips()
    {
        var store = _fixture.NewStore("settings_null");
        await store.EnsureIndexesAsync();
        var counters = new CounterService(store);
        var repo = new ModuleSettingsRepository(store, counters);

        await repo.UpdateModuleSettingAsync(Guid.NewGuid(), 7, "K", null);
        var doc = await repo.GetModuleSettingsAsync(7);
        Assert.Equal("", doc["K"]); // hashtable semantics: null -> empty string
    }

    [Fact]
    public async Task Page_modules_filtered_by_publish_window_and_ordered()
    {
        var store = _fixture.NewStore("pagemods");
        await store.EnsureIndexesAsync();
        var counters = new CounterService(store);
        var repo = new ModuleRepository(store, counters);
        var now = DateTime.UtcNow;

        var m1 = new Module { SiteId = 1, ModuleTitle = "m1" };
        m1.ModuleId = await repo.CreateAsync(m1);
        var m2 = new Module { SiteId = 1, ModuleTitle = "m2" };
        m2.ModuleId = await repo.CreateAsync(m2);
        var future = new Module { SiteId = 1, ModuleTitle = "future" };
        future.ModuleId = await repo.CreateAsync(future);
        var expired = new Module { SiteId = 1, ModuleTitle = "expired" };
        expired.ModuleId = await repo.CreateAsync(expired);

        int pageId = 42;
        await store.PageModules.InsertManyAsync(
        [
            new PageModule { PageId = pageId, ModuleId = m2.ModuleId, PaneName = "leftpane", ModuleOrder = 2 },
            new PageModule { PageId = pageId, ModuleId = m1.ModuleId, PaneName = "contentpane", ModuleOrder = 1 },
            new PageModule { PageId = pageId, ModuleId = future.ModuleId, PaneName = "contentpane", ModuleOrder = 3, PublishBeginDate = now.AddDays(1) },
            new PageModule { PageId = pageId, ModuleId = expired.ModuleId, PaneName = "contentpane", ModuleOrder = 4, PublishEndDate = now.AddDays(-1) },
        ]);

        var modules = await repo.GetPageModulesAsync(pageId, now);
        Assert.Equal(new[] { m1.ModuleId, m2.ModuleId }, modules.Select(m => m.ModuleId).ToArray()); // ModuleOrder sort
        Assert.Equal("contentpane", modules[0].PaneName);
        Assert.Equal("leftpane", modules[1].PaneName);
    }

    [Fact]
    public async Task Page_lookup_by_url_id_default_and_friendly()
    {
        var store = _fixture.NewStore("pages");
        await store.EnsureIndexesAsync();
        var repo = new PageRepository(store);

        await store.Pages.InsertManyAsync(
        [
            new SitePage { PageId = 1, SiteId = 1, PageName = "Home", Url = "~/home", PageOrder = 0 },
            new SitePage { PageId = 2, SiteId = 1, PageName = "About", Url = "~/about", PageOrder = 1 },
            new SitePage { PageId = 3, SiteId = 2, PageName = "Home", Url = "~/home", PageOrder = 0 },
        ]);
        await store.FriendlyUrls.InsertOneAsync(new FriendlyUrl
        {
            Id = 1, SiteId = 1, FriendlyUrlName = "my-alias", RealUrl = "~/Default.aspx?pageid=2",
        });

        Assert.Equal(2, (await repo.GetByUrlAsync(1, "~/about"))!.PageId);
        Assert.Equal(1, (await repo.GetDefaultPageAsync(1))!.PageId);
        Assert.Equal(3, (await repo.GetDefaultPageAsync(2))!.PageId);
        Assert.Equal(2, await repo.ResolvePageIdByFriendlyUrlAsync(1, "my-alias"));
        Assert.Equal("About", (await repo.GetByIdAsync(2))!.PageName);
        Assert.Null(await repo.GetByUrlAsync(1, "~/nope"));
    }

    [Fact]
    public async Task Update_last_modified_stamps_page()
    {
        var store = _fixture.NewStore("pagelastmod");
        await store.EnsureIndexesAsync();
        var repo = new PageRepository(store);
        await store.Pages.InsertOneAsync(new SitePage { PageId = 9, SiteId = 1, Url = "~/x" });

        var when = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await repo.UpdateLastModifiedAsync(9, when);
        var page = await repo.GetByIdAsync(9);
        Assert.Equal(when, Utc(page!.LastModifiedUtc));
    }

    [Fact]
    public async Task User_lookups_do_not_filter_isdeleted()
    {
        var store = _fixture.NewStore("users");
        await store.EnsureIndexesAsync();
        var repo = new UserRepository(store);

        await store.Users.InsertManyAsync(
        [
            new SiteUser { UserId = 1, SiteId = 1, LoginName = "del", Email = "d@x", LoweredEmail = "d@x", IsDeleted = true },
            new SiteUser { UserId = 2, SiteId = 1, LoginName = "live", Email = "L@X", LoweredEmail = "l@x" },
        ]);

        // mp_Users_SelectByLoginName/ByEmail/ByID have no IsDeleted filter.
        Assert.NotNull(await repo.GetByLoginNameAsync(1, "del"));
        Assert.NotNull(await repo.GetByEmailAsync(1, "d@x"));
        Assert.Equal(1, (await repo.GetByIdAsync(1))!.UserId);

        // Email lookup is case-insensitive via loweredemail.
        Assert.Equal(2, (await repo.GetByEmailAsync(1, "l@x"))!.UserId);
    }

    [Fact]
    public async Task Increment_password_attempts_window_logic()
    {
        var store = _fixture.NewStore("userattempts");
        await store.EnsureIndexesAsync();
        var repo = new UserRepository(store);
        // MongoDB stores datetimes at millisecond precision; use a
        // millisecond-aligned timestamp so equality comparisons hold.
        var now = new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond
            * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

        await store.Users.InsertOneAsync(new SiteUser
        {
            UserId = 1, SiteId = 1, FailedPasswordAttemptCount = 0,
            FailedPasswordAttemptWindowStart = DateTime.MinValue,
        });

        // First failure: window starts now, count = 1.
        await repo.IncrementPasswordAttemptsAsync(1, 5, 30, now);
        var u = await repo.GetByIdAsync(1);
        Assert.Equal(1, u!.FailedPasswordAttemptCount);
        Assert.Equal(Utc(now), Utc(u.FailedPasswordAttemptWindowStart));

        // Second failure inside window: count = 2, same window start.
        await repo.IncrementPasswordAttemptsAsync(1, 5, 30, now.AddMinutes(5));
        u = await repo.GetByIdAsync(1);
        Assert.Equal(2, u!.FailedPasswordAttemptCount);
        Assert.Equal(Utc(now), Utc(u.FailedPasswordAttemptWindowStart));

        // Failure after window expiry: count resets to 1, new window.
        await repo.IncrementPasswordAttemptsAsync(1, 5, 30, now.AddHours(2));
        u = await repo.GetByIdAsync(1);
        Assert.Equal(1, u!.FailedPasswordAttemptCount);
        Assert.Equal(Utc(now.AddHours(2)), Utc(u.FailedPasswordAttemptWindowStart));
    }

    [Fact]
    public async Task Role_names_for_user()
    {
        var store = _fixture.NewStore("userroles");
        await store.EnsureIndexesAsync();
        var repo = new UserRepository(store);
        await store.Roles.InsertManyAsync(
        [
            new Role { RoleId = 1, SiteId = 1, RoleName = "Admins" },
            new Role { RoleId = 2, SiteId = 1, RoleName = "Editors" },
        ]);
        await store.UserRoles.InsertManyAsync(
        [
            new UserRole { Id = 1, UserId = 9, RoleId = 1 },
            new UserRole { Id = 2, UserId = 9, RoleId = 2 },
        ]);
        var names = await repo.GetRoleNamesForUserAsync(9);
        Assert.Equal(new[] { "Admins", "Editors" }, names.OrderBy(n => n).ToArray());
    }
}
