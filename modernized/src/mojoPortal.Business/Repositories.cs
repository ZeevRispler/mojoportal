// Persistence contracts. Implementations live in mojoPortal.Data.Mongo.
namespace mojoPortal.Business;

public interface ISiteRepository
{
    Task<Site?> GetFirstSiteAsync(CancellationToken ct = default);
    Task<Site?> GetByIdAsync(int siteId, CancellationToken ct = default);
}

public interface IPageRepository
{
    Task<SitePage?> GetByIdAsync(int pageId, CancellationToken ct = default);
    Task<SitePage?> GetByUrlAsync(int siteId, string url, CancellationToken ct = default);
    Task<int?> ResolvePageIdByFriendlyUrlAsync(int siteId, string friendlyUrl, CancellationToken ct = default);
    Task<SitePage?> GetDefaultPageAsync(int siteId, CancellationToken ct = default);
    Task UpdateLastModifiedAsync(int pageId, DateTime whenUtc, CancellationToken ct = default);
}

public interface IModuleRepository
{
    Task<Module?> GetByIdAsync(int moduleId, CancellationToken ct = default);
    Task<List<Module>> GetPageModulesAsync(int pageId, DateTime currentTimeUtc, CancellationToken ct = default);
    Task<List<Module>> GetModulesForSiteAsync(int siteId, Guid featureGuid, CancellationToken ct = default);
    Task<int> CreateAsync(Module module, CancellationToken ct = default);
    Task<bool> DeleteAsync(int moduleId, CancellationToken ct = default);
}

public interface IModuleSettingsRepository
{
    Task<Dictionary<string, string>> GetModuleSettingsAsync(int moduleId, CancellationToken ct = default);
    Task UpdateModuleSettingAsync(Guid moduleGuid, int moduleId, string settingName, string? value, CancellationToken ct = default);
    Task DeleteByModuleAsync(int moduleId, CancellationToken ct = default);
}

public interface ILinkRepository
{
    Task<int> AddLinkAsync(Link link, CancellationToken ct = default);
    Task<bool> UpdateLinkAsync(Link link, CancellationToken ct = default);
    Task<bool> DeleteLinkAsync(int itemId, CancellationToken ct = default);
    Task<bool> DeleteByModuleAsync(int moduleId, CancellationToken ct = default);
    Task<bool> DeleteBySiteAsync(int siteId, CancellationToken ct = default);
    Task<Link?> GetLinkAsync(int itemId, CancellationToken ct = default);
    Task<List<Link>> GetLinksAsync(int moduleId, CancellationToken ct = default);
    Task<int> GetCountAsync(int moduleId, CancellationToken ct = default);
    Task<(List<Link> Items, int TotalPages)> GetPageAsync(int moduleId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<List<Link>> GetLinksByPageAsync(int siteId, int pageId, CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<SiteUser?> GetByLoginNameAsync(int siteId, string loginName, CancellationToken ct = default);
    Task<SiteUser?> GetByEmailAsync(int siteId, string email, CancellationToken ct = default);
    Task<SiteUser?> GetByIdAsync(int userId, CancellationToken ct = default);
    Task<List<string>> GetRoleNamesForUserAsync(int userId, CancellationToken ct = default);
    Task UpdateLastLoginAsync(int userId, DateTime whenUtc, CancellationToken ct = default);

    /// <summary>SiteUser.IncrementPasswordAttempts: FailedPasswordAttemptCount+1
    /// (or reset to 1 with new window start) inside the attempt window.</summary>
    Task IncrementPasswordAttemptsAsync(int userId, int maxAttempts, int windowMinutes, DateTime nowUtc, CancellationToken ct = default);
}

public interface ICounterRepository
{
    Task<int> NextAsync(string name, int seed, CancellationToken ct = default);
}
