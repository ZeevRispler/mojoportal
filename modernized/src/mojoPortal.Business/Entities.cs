// Entity shapes mirror the legacy T-SQL schema (mp_* tables) at source commit
// 2205a743. Field names preserve the original column names.
namespace mojoPortal.Business;

public sealed class Site
{
    public int SiteId { get; set; } = -1;
    public Guid SiteGuid { get; set; } = Guid.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string Skin { get; set; } = string.Empty;
    public bool UseSslOnAllPages { get; set; }
    public bool AllowNewRegistration { get; set; } = true;
    public bool RequiresUniqueEmail { get; set; }
    // SiteRootEditRoles is only consulted when related-sites mode is enabled.
    public string SiteRootEditRoles { get; set; } = string.Empty;

    // login/security settings consulted by SiteLogin / mojoMembershipProvider
    public bool UseEmailForLogin { get; set; } = true;
    public bool UseSecureRegistration { get; set; }
    public bool RequireApprovalBeforeLogin { get; set; }
    public int MaxInvalidPasswordAttempts { get; set; }
    public int PasswordAttemptWindowMinutes { get; set; } = 30;
}

public sealed class SitePage
{
    public int PageId { get; set; } = -1;
    public int ParentId { get; set; } = -1;
    public int PageOrder { get; set; }
    public int SiteId { get; set; }
    public string PageName { get; set; } = string.Empty;
    public string PageTitle { get; set; } = string.Empty;
    public string AuthorizedRoles { get; set; } = "All Users;";
    public string EditRoles { get; set; } = string.Empty;
    public bool RequireSsl { get; set; }
    public string Url { get; set; } = string.Empty;
    public Guid PageGuid { get; set; } = Guid.Empty;
    public Guid SiteGuid { get; set; } = Guid.Empty;
    public DateTime LastModifiedUtc { get; set; } = DateTime.MinValue;
    public List<Module> Modules { get; set; } = new();
}

public sealed class FriendlyUrl
{
    public int Id { get; set; } = -1;
    public int SiteId { get; set; }
    public string FriendlyUrlName { get; set; } = string.Empty;
    public string RealUrl { get; set; } = string.Empty;
    public Guid PageGuid { get; set; } = Guid.Empty;
    public bool IsPattern { get; set; }
}

public sealed class ModuleDefinition
{
    public int ModuleDefId { get; set; } = -1;
    public string FeatureName { get; set; } = string.Empty;
    public string ControlSrc { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsAdmin { get; set; }
    public Guid Guid { get; set; } = Guid.Empty; // FeatureGuid
}

public sealed class Module
{
    public int ModuleId { get; set; } = -1;
    public int SiteId { get; set; }
    public int ModuleDefId { get; set; }
    public string ModuleTitle { get; set; } = string.Empty;
    public string AuthorizedEditRoles { get; set; } = string.Empty;
    public string DraftEditRoles { get; set; } = string.Empty;
    public string DraftApprovalRoles { get; set; } = string.Empty;
    public int CacheTime { get; set; }
    public bool ShowTitle { get; set; } = true;
    public int EditUserId { get; set; } = -1;
    public Guid EditUserGuid { get; set; } = Guid.Empty;
    public int CreatedByUserId { get; set; } = -1;
    public DateTime CreatedDate { get; set; } = DateTime.MinValue;
    public Guid ModuleGuid { get; set; } = Guid.Empty;
    public Guid FeatureGuid { get; set; } = Guid.Empty;
    public Guid SiteGuid { get; set; } = Guid.Empty;
    public bool HideFromUnauthenticated { get; set; }
    public bool HideFromAuthenticated { get; set; }
    public string ViewRoles { get; set; } = "All Users;";
    public bool IncludeInSearch { get; set; } = true;
    public bool IsGlobal { get; set; }
    public string HeadElement { get; set; } = "h2";

    // placement fields populated when loaded via page context (mp_PageModules)
    public int PageId { get; set; } = -1;
    public string PaneName { get; set; } = string.Empty;
    public int ModuleOrder { get; set; } = 999;
    public string ControlSource { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public DateTime? PublishBeginDate { get; set; }
    public DateTime? PublishEndDate { get; set; }
}

public sealed class PageModule
{
    public int PageId { get; set; }
    public int ModuleId { get; set; }
    public string PaneName { get; set; } = "contentpane";
    public int ModuleOrder { get; set; } = 999;
    public DateTime PublishBeginDate { get; set; } = DateTime.MinValue;
    public DateTime? PublishEndDate { get; set; }
}

public sealed class ModuleSetting
{
    public int Id { get; set; } = -1;
    public Guid SettingGuid { get; set; } = Guid.Empty;
    public int ModuleId { get; set; }
    public Guid ModuleGuid { get; set; } = Guid.Empty;
    public string SettingName { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
    public string ControlType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class Link
{
    public static readonly Guid FeatureGuid = new("74bdbcc2-0e79-47ff-bcd4-a159270bf36e");

    public int ItemId { get; set; } = -1;
    public int ModuleId { get; set; } = -1;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public int ViewOrder { get; set; } = 500;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public int CreatedByUser { get; set; } = -1;
    public Guid ItemGuid { get; set; } = Guid.Empty;
    public Guid ModuleGuid { get; set; } = Guid.Empty;
    public Guid UserGuid { get; set; } = Guid.Empty;
}

public sealed class Role
{
    public int RoleId { get; set; } = -1;
    public int SiteId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public Guid RoleGuid { get; set; } = Guid.Empty;
}

public sealed class SiteUser
{
    public int UserId { get; set; } = -1;
    public int SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? LoweredEmail { get; set; }
    public string Pwd { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    /// <summary>Source mp_Users.PasswordFormat int (0 clear, 1 hashed, 2 encrypted).</summary>
    public int PasswordFormat { get; set; } = 1;
    public Guid UserGuid { get; set; } = Guid.Empty;
    public bool IsDeleted { get; set; }
    public bool IsLockedOut { get; set; }
    public bool ApprovedForLogin { get; set; } = true;
    public Guid RegisterConfirmGuid { get; set; } = Guid.Empty;
    public int FailedPasswordAttemptCount { get; set; }
    public DateTime FailedPasswordAttemptWindowStart { get; set; } = DateTime.MinValue;
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public DateTime? LastLockoutDate { get; set; }
    public List<string> Roles { get; set; } = new();
}

public sealed class UserRole
{
    public int Id { get; set; } = -1;
    public int UserId { get; set; }
    public int RoleId { get; set; }
}
