using MongoDB.Driver;
using mojoPortal.Business;

namespace mojoPortal.Data.Mongo;

/// <summary>
/// Case-insensitive ordering to match the source's default SQL collation
/// (SQL_Latin1_General_CP1_CI_AS style ORDER BY ViewOrder, Title).
/// </summary>
internal static class CollationDefaults
{
    internal static readonly MongoDB.Driver.Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);
}

public sealed class LinkRepository : ILinkRepository
{
    private readonly MongoStore _store;
    private readonly ICounterRepository _counters;

    public LinkRepository(MongoStore store, ICounterRepository counters)
    {
        _store = store;
        _counters = counters;
    }

    public async Task<int> AddLinkAsync(Link link, CancellationToken ct = default)
    {
        link.ItemId = await _counters.NextAsync("mp_Links", seed: 0, ct);
        await _store.Links.InsertOneAsync(link, cancellationToken: ct);
        return link.ItemId;
    }

    public async Task<bool> UpdateLinkAsync(Link link, CancellationToken ct = default)
    {
        // mp_Links_Update touches exactly these fields.
        var update = Builders<Link>.Update
            .Set(l => l.ModuleId, link.ModuleId)
            .Set(l => l.Title, link.Title)
            .Set(l => l.Url, link.Url)
            .Set(l => l.ViewOrder, link.ViewOrder)
            .Set(l => l.Description, link.Description)
            .Set(l => l.CreatedDate, link.CreatedDate)
            .Set(l => l.CreatedByUser, link.CreatedByUser)
            .Set(l => l.Target, link.Target);

        var result = await _store.Links.UpdateOneAsync(l => l.ItemId == link.ItemId, update, cancellationToken: ct);
        // Source returns (rowsAffected > -1): true even when nothing matched.
        return result.IsAcknowledged;
    }

    public async Task<bool> DeleteLinkAsync(int itemId, CancellationToken ct = default)
    {
        var result = await _store.Links.DeleteOneAsync(l => l.ItemId == itemId, ct);
        return result.IsAcknowledged;
    }

    public async Task<bool> DeleteByModuleAsync(int moduleId, CancellationToken ct = default)
    {
        var result = await _store.Links.DeleteManyAsync(l => l.ModuleId == moduleId, ct);
        return result.IsAcknowledged;
    }

    public async Task<bool> DeleteBySiteAsync(int siteId, CancellationToken ct = default)
    {
        // mp_Links_DeleteBySite: WHERE ModuleID IN (SELECT ModuleID FROM mp_Modules WHERE SiteID = @SiteID)
        var moduleIds = await _store.Modules
            .Find(m => m.SiteId == siteId)
            .Project(m => m.ModuleId)
            .ToListAsync(ct);

        if (moduleIds.Count == 0)
        {
            return true;
        }

        var result = await _store.Links.DeleteManyAsync(
            Builders<Link>.Filter.In(l => l.ModuleId, moduleIds), ct);
        return result.IsAcknowledged;
    }

    public Task<Link?> GetLinkAsync(int itemId, CancellationToken ct = default)
        => _store.Links.Find(l => l.ItemId == itemId).FirstOrDefaultAsync(ct)!;

    public async Task<List<Link>> GetLinksAsync(int moduleId, CancellationToken ct = default)
    {
        // mp_Links_Select: WHERE ModuleID ORDER BY ViewOrder, Title
        var find = _store.Links.Find(l => l.ModuleId == moduleId);
        find.Options.Collation = CollationDefaults.CaseInsensitive;
        return await find
            .Sort(Builders<Link>.Sort.Ascending(l => l.ViewOrder).Ascending(l => l.Title))
            .ToListAsync(ct);
    }

    public async Task<int> GetCountAsync(int moduleId, CancellationToken ct = default)
        => (int)await _store.Links.CountDocumentsAsync(l => l.ModuleId == moduleId, cancellationToken: ct);

    public async Task<(List<Link> Items, int TotalPages)> GetPageAsync(
        int moduleId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        int totalRows = await GetCountAsync(moduleId, ct);
        int totalPages = LinkPaging.ComputeTotalPages(totalRows, pageSize);
        (int skip, int take) = LinkPaging.ComputeWindow(pageNumber, pageSize);

        if (take <= 0)
        {
            return (new List<Link>(), totalPages);
        }

        var find = _store.Links.Find(l => l.ModuleId == moduleId);
        find.Options.Collation = CollationDefaults.CaseInsensitive;
        var items = await find
            .Sort(Builders<Link>.Sort.Ascending(l => l.ViewOrder).Ascending(l => l.Title))
            .Skip(skip)
            .Limit(take)
            .ToListAsync(ct);

        return (items, totalPages);
    }

    public async Task<List<Link>> GetLinksByPageAsync(int siteId, int pageId, CancellationToken ct = default)
    {
        // mp_Links_SelectByPage: links whose module sits on the given page of
        // the given site (join via mp_PageModules / mp_Modules / mp_Pages).
        var page = await _store.Pages.Find(p => p.PageId == pageId && p.SiteId == siteId).FirstOrDefaultAsync(ct);
        if (page is null)
        {
            return new List<Link>();
        }

        var moduleIds = await _store.PageModules
            .Find(pm => pm.PageId == pageId)
            .Project(pm => pm.ModuleId)
            .ToListAsync(ct);

        if (moduleIds.Count == 0)
        {
            return new List<Link>();
        }

        var find = _store.Links.Find(Builders<Link>.Filter.In(l => l.ModuleId, moduleIds));
        find.Options.Collation = CollationDefaults.CaseInsensitive;
        return await find
            .Sort(Builders<Link>.Sort.Ascending(l => l.ViewOrder).Ascending(l => l.Title))
            .ToListAsync(ct);
    }
}

public sealed class SiteRepository : ISiteRepository
{
    private readonly MongoStore _store;
    public SiteRepository(MongoStore store) => _store = store;

    public Task<Site?> GetFirstSiteAsync(CancellationToken ct = default)
        => _store.Sites.Find(_ => true).SortBy(s => s.SiteId).FirstOrDefaultAsync(ct)!;

    public Task<Site?> GetByIdAsync(int siteId, CancellationToken ct = default)
        => _store.Sites.Find(s => s.SiteId == siteId).FirstOrDefaultAsync(ct)!;
}

public sealed class PageRepository : IPageRepository
{
    private readonly MongoStore _store;
    public PageRepository(MongoStore store) => _store = store;

    public Task<SitePage?> GetByIdAsync(int pageId, CancellationToken ct = default)
        => _store.Pages.Find(p => p.PageId == pageId).FirstOrDefaultAsync(ct)!;

    public Task<SitePage?> GetByUrlAsync(int siteId, string url, CancellationToken ct = default)
        => _store.Pages.Find(p => p.SiteId == siteId && p.Url == url).FirstOrDefaultAsync(ct)!;

    public async Task<int?> ResolvePageIdByFriendlyUrlAsync(int siteId, string friendlyUrl, CancellationToken ct = default)
    {
        // mp_FriendlyUrls map friendly name -> real url like ~/Default.aspx?pageid=N
        var fu = await _store.FriendlyUrls
            .Find(f => f.SiteId == siteId && f.FriendlyUrlName == friendlyUrl)
            .FirstOrDefaultAsync(ct);

        if (fu is null) { return null; }

        const string marker = "pageid=";
        int idx = fu.RealUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) { return null; }

        var rest = fu.RealUrl[(idx + marker.Length)..];
        int amp = rest.IndexOf('&');
        if (amp >= 0) { rest = rest[..amp]; }

        return int.TryParse(rest, out int pageId) ? pageId : null;
    }

    public async Task<SitePage?> GetDefaultPageAsync(int siteId, CancellationToken ct = default)
        => await _store.Pages
            .Find(p => p.SiteId == siteId)
            .SortBy(p => p.PageOrder)
            .FirstOrDefaultAsync(ct);

    public async Task UpdateLastModifiedAsync(int pageId, DateTime whenUtc, CancellationToken ct = default)
        => await _store.Pages.UpdateOneAsync(
            p => p.PageId == pageId,
            Builders<SitePage>.Update.Set(p => p.LastModifiedUtc, whenUtc),
            cancellationToken: ct);
}

public sealed class ModuleRepository : IModuleRepository
{
    private readonly MongoStore _store;
    private readonly ICounterRepository _counters;

    public ModuleRepository(MongoStore store, ICounterRepository counters)
    {
        _store = store;
        _counters = counters;
    }

    public Task<Module?> GetByIdAsync(int moduleId, CancellationToken ct = default)
        => _store.Modules.Find(m => m.ModuleId == moduleId).FirstOrDefaultAsync(ct)!;

    public async Task<List<Module>> GetPageModulesAsync(int pageId, DateTime currentTimeUtc, CancellationToken ct = default)
    {
        // mp_Modules_SelectByPage: INNER JOIN mp_PageModules pm
        // WHERE pm.PageID = @PageID
        //   AND pm.PublishBeginDate <= @CurrentTime
        //   AND (pm.PublishEndDate IS NULL OR pm.PublishEndDate > @CurrentTime)
        // ORDER BY pm.ModuleOrder
        var filter = Builders<PageModule>.Filter.Eq(pm => pm.PageId, pageId)
            & Builders<PageModule>.Filter.Lte(pm => pm.PublishBeginDate, currentTimeUtc)
            & (Builders<PageModule>.Filter.Eq(pm => pm.PublishEndDate, (DateTime?)null)
               | Builders<PageModule>.Filter.Gt(pm => pm.PublishEndDate, currentTimeUtc));

        var placements = await _store.PageModules
            .Find(filter)
            .SortBy(pm => pm.ModuleOrder)
            .ToListAsync(ct);

        var result = new List<Module>(placements.Count);
        foreach (var pm in placements)
        {
            var module = await _store.Modules.Find(m => m.ModuleId == pm.ModuleId).FirstOrDefaultAsync(ct);
            if (module is null) { continue; }

            var def = await _store.ModuleDefinitions.Find(d => d.ModuleDefId == module.ModuleDefId).FirstOrDefaultAsync(ct);
            module.PageId = pageId;
            module.PaneName = pm.PaneName;
            module.ModuleOrder = pm.ModuleOrder;
            module.PublishBeginDate = pm.PublishBeginDate;
            module.PublishEndDate = pm.PublishEndDate;
            module.ControlSource = def?.ControlSrc ?? string.Empty;
            module.FeatureName = def?.FeatureName ?? string.Empty;
            if (def is not null)
            {
                module.FeatureGuid = def.Guid;
            }

            result.Add(module);
        }

        return result;
    }

    public async Task<List<Module>> GetModulesForSiteAsync(int siteId, Guid featureGuid, CancellationToken ct = default)
        => await _store.Modules
            .Find(m => m.SiteId == siteId && m.FeatureGuid == featureGuid)
            .ToListAsync(ct);

    public async Task<int> CreateAsync(Module module, CancellationToken ct = default)
    {
        module.ModuleId = await _counters.NextAsync("mp_Modules", seed: 0, ct);
        if (module.ModuleGuid == Guid.Empty)
        {
            module.ModuleGuid = Guid.NewGuid();
        }
        await _store.Modules.InsertOneAsync(module, cancellationToken: ct);
        return module.ModuleId;
    }

    public async Task<bool> DeleteAsync(int moduleId, CancellationToken ct = default)
    {
        await _store.PageModules.DeleteManyAsync(pm => pm.ModuleId == moduleId, ct);
        var result = await _store.Modules.DeleteOneAsync(m => m.ModuleId == moduleId, ct);
        return result.IsAcknowledged;
    }
}

public sealed class ModuleSettingsRepository : IModuleSettingsRepository
{
    private readonly MongoStore _store;
    private readonly ICounterRepository _counters;

    public ModuleSettingsRepository(MongoStore store, ICounterRepository counters)
    {
        _store = store;
        _counters = counters;
    }

    public async Task<Dictionary<string, string>> GetModuleSettingsAsync(int moduleId, CancellationToken ct = default)
    {
        var rows = await _store.ModuleSettings
            .Find(s => s.ModuleId == moduleId)
            .ToListAsync(ct);

        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            settings[row.SettingName] = row.SettingValue ?? string.Empty;
        }

        return settings;
    }

    public async Task UpdateModuleSettingAsync(
        Guid moduleGuid, int moduleId, string settingName, string? value, CancellationToken ct = default)
    {
        // ModuleSettings.UpdateModuleSetting: updates the row when it exists,
        // inserts it when it doesn't.
        var filter = Builders<ModuleSetting>.Filter.Eq(s => s.ModuleId, moduleId)
            & Builders<ModuleSetting>.Filter.Eq(s => s.SettingName, settingName);

        var existing = await _store.ModuleSettings.Find(filter).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            var setting = new ModuleSetting
            {
                Id = await _counters.NextAsync("mp_ModuleSettings", seed: 1, ct),
                SettingGuid = Guid.NewGuid(),
                ModuleId = moduleId,
                ModuleGuid = moduleGuid,
                SettingName = settingName,
                SettingValue = value,
                ControlType = string.Empty,
                SortOrder = 0,
            };
            await _store.ModuleSettings.InsertOneAsync(setting, cancellationToken: ct);
        }
        else
        {
            await _store.ModuleSettings.UpdateOneAsync(
                s => s.Id == existing.Id,
                Builders<ModuleSetting>.Update.Set(s => s.SettingValue, value),
                cancellationToken: ct);
        }
    }

    public async Task DeleteByModuleAsync(int moduleId, CancellationToken ct = default)
    {
        await _store.ModuleSettings.DeleteManyAsync(s => s.ModuleId == moduleId, ct);
    }
}

public sealed class UserRepository : IUserRepository
{
    private readonly MongoStore _store;

    public UserRepository(MongoStore store) => _store = store;

    // mp_Users_SelectByLoginName / _SelectByEmail / _SelectByID select all rows,
    // including deleted ones; the SiteLogin gate checks IsDeleted itself.
    public Task<SiteUser?> GetByLoginNameAsync(int siteId, string loginName, CancellationToken ct = default)
        => _store.Users.Find(u => u.SiteId == siteId && u.LoginName == loginName).FirstOrDefaultAsync(ct)!;

    public Task<SiteUser?> GetByEmailAsync(int siteId, string email, CancellationToken ct = default)
        => _store.Users.Find(u => u.SiteId == siteId && u.LoweredEmail == email.ToLowerInvariant()).FirstOrDefaultAsync(ct)!;

    public Task<SiteUser?> GetByIdAsync(int userId, CancellationToken ct = default)
        => _store.Users.Find(u => u.UserId == userId).FirstOrDefaultAsync(ct)!;

    public async Task<List<string>> GetRoleNamesForUserAsync(int userId, CancellationToken ct = default)
    {
        var roleIds = await _store.UserRoles
            .Find(ur => ur.UserId == userId)
            .Project(ur => ur.RoleId)
            .ToListAsync(ct);

        if (roleIds.Count == 0)
        {
            return new List<string>();
        }

        var roles = await _store.Roles
            .Find(Builders<Role>.Filter.In(r => r.RoleId, roleIds))
            .ToListAsync(ct);

        return roles.Select(r => r.RoleName).ToList();
    }

    public async Task UpdateLastLoginAsync(int userId, DateTime whenUtc, CancellationToken ct = default)
        => await _store.Users.UpdateOneAsync(
            u => u.UserId == userId,
            Builders<SiteUser>.Update.Set(u => u.LastLoginDate, whenUtc),
            cancellationToken: ct);

    public async Task IncrementPasswordAttemptsAsync(
        int userId, int maxAttempts, int windowMinutes, DateTime nowUtc, CancellationToken ct = default)
    {
        var user = await GetByIdAsync(userId, ct);
        if (user is null) { return; }

        // SiteUser.IncrementPasswordAttempts
        if (user.FailedPasswordAttemptCount == 0
            || user.FailedPasswordAttemptWindowStart.AddMinutes(windowMinutes) < nowUtc)
        {
            await _store.Users.UpdateOneAsync(
                u => u.UserId == userId,
                Builders<SiteUser>.Update
                    .Set(u => u.FailedPasswordAttemptWindowStart, nowUtc)
                    .Set(u => u.FailedPasswordAttemptCount, 1),
                cancellationToken: ct);
        }
        else
        {
            await _store.Users.UpdateOneAsync(
                u => u.UserId == userId,
                Builders<SiteUser>.Update.Set(u => u.FailedPasswordAttemptCount, user.FailedPasswordAttemptCount + 1),
                cancellationToken: ct);
        }
    }
}
