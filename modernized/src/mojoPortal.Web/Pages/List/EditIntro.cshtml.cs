using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using mojoPortal.Business;
using mojoPortal.Business.Authorization;
using mojoPortal.Web.Services;

namespace mojoPortal.Web.Pages.List;

/// <summary>Port of EditIntro.aspx.cs: edits the module's IntroContent setting.</summary>
public class EditIntroModel : PageModel
{
    private readonly MojoContext _ctx;
    private readonly PermissionService _perms;
    private readonly IMojoUser _user;
    private readonly IModuleRepository _modules;
    private readonly IModuleSettingsRepository _moduleSettings;

    public EditIntroModel(MojoContext ctx, PermissionService perms, IMojoUser user,
        IModuleRepository modules, IModuleSettingsRepository moduleSettings)
    {
        _ctx = ctx;
        _perms = perms;
        _user = user;
        _modules = modules;
        _moduleSettings = moduleSettings;
    }

    public int PageId { get; private set; } = -1;
    public int ModuleId { get; private set; } = -1;
    public string CancelUrl { get; private set; } = string.Empty;
    private SitePage? _currentPage;

    [BindProperty]
    public string? IntroContent { get; set; }

    private async Task<IActionResult?> GateAsync(int pageId, int moduleId, CancellationToken ct)
    {
        PageId = pageId;
        ModuleId = moduleId;

        if (!_user.IsAuthenticated)
        {
            return Challenge();
        }

        _currentPage = await _ctx.GetPageByIdAsync(pageId, ct);
        if (!_perms.UserCanEditModule(_user, _currentPage, moduleId, Link.FeatureGuid))
        {
            return Redirect("/AccessDenied.aspx");
        }

        CancelUrl = MojoContext.GetCurrentPageUrl(_currentPage);
        return null;
    }

    public async Task<IActionResult> OnGetAsync(int pageId, int mid, CancellationToken ct = default)
    {
        var gate = await GateAsync(pageId, mid, ct);
        if (gate is not null) { return gate; }

        var settings = await _ctx.GetModuleSettingsAsync(ModuleId, ct);
        var config = new ListConfiguration(settings);
        IntroContent = config.IntroContent;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int pageId, int mid, CancellationToken ct = default)
    {
        var gate = await GateAsync(pageId, mid, ct);
        if (gate is not null) { return gate; }

        // NonCmsBasePage sanitizes textarea content for non-admins.
        var content = WebUser.IsAdmin(_user) ? IntroContent : MarkupHelper.SanitizeMarkup(IntroContent);

        var module = await _modules.GetByIdAsync(ModuleId, ct);
        if (module is not null)
        {
            await _moduleSettings.UpdateModuleSettingAsync(
                module.ModuleGuid, ModuleId, "IntroContent", content, ct);
        }

        return Redirect(CancelUrl);
    }
}
