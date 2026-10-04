using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using mojoPortal.Business;
using mojoPortal.Business.Authorization;
using mojoPortal.Web.Services;

namespace mojoPortal.Web.Pages.List;

/// <summary>
/// Port of Edit.aspx.cs (Links item editor). NonCmsBasePage: every POST
/// runs EncodeTextboxControls — single-line inputs get RemoveMarkup(),
/// Description gets SanitizeMarkup() unless the user is an Admin.
/// </summary>
public class EditModel : PageModel
{
    private readonly MojoContext _ctx;
    private readonly PermissionService _perms;
    private readonly IMojoUser _user;
    private readonly ILinkRepository _links;
    private readonly IModuleRepository _modules;
    private readonly IPageRepository _pages;
    private readonly LinkService _linkService;
    private readonly IUserRepository _users;
    private readonly IConfiguration _appConfig;

    public EditModel(MojoContext ctx, PermissionService perms, IMojoUser user,
        ILinkRepository links, IModuleRepository modules, IPageRepository pages,
        LinkService linkService, IUserRepository users, IConfiguration appConfig)
    {
        _ctx = ctx;
        _perms = perms;
        _user = user;
        _links = links;
        _modules = modules;
        _pages = pages;
        _linkService = linkService;
        _users = users;
        _appConfig = appConfig;
    }

    public int PageId { get; private set; } = -1;
    public int ModuleId { get; private set; } = -1;
    private SitePage? _currentPage;
    public int ItemId { get; private set; } = -1;
    public bool DescriptionOnly { get; private set; }
    public bool UseProtocolDropdown { get; private set; }
    public string? CreatedByUserName { get; private set; }
    public string CancelUrl { get; private set; } = string.Empty;
    public List<string> Errors { get; } = new();
    public string DeleteConfirmText => "Are you sure you want to delete this link?";

    [BindProperty]
    public string? Title { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string? UrlText { get; set; }

    [BindProperty]
    public string? ViewOrderText { get; set; }

    [BindProperty]
    public bool UseNewWindow { get; set; }

    [BindProperty]
    public string? Protocol { get; set; }

    [BindProperty]
    public string HdnReturnUrl { get; set; } = string.Empty;

    private async Task<IActionResult?> GateAsync(int pageId, int moduleId, CancellationToken ct)
    {
        PageId = pageId;
        ModuleId = moduleId;

        // Source: Request.IsAuthenticated -> login redirect happens via
        // FormsAuth; ASP.NET Core cookie auth redirects automatically when
        // we issue Challenge(). The login page gets ?returnurl=<here>.
        if (!_user.IsAuthenticated)
        {
            return Challenge();
        }

        _currentPage = await _ctx.GetPageByIdAsync(pageId, ct);
        if (!_perms.UserCanEditModule(_user, _currentPage, moduleId, Link.FeatureGuid))
        {
            return Redirect("/AccessDenied.aspx");
        }

        var settings = await _ctx.GetModuleSettingsAsync(moduleId, ct);
        var config = new ListConfiguration(settings);
        DescriptionOnly = config.DescriptionOnly;
        UseProtocolDropdown = _appConfig.GetValue("List:UseProtocolDropdown", false);

        return null;
    }

    public async Task<IActionResult> OnGetAsync(int pageId, int mid, int itemId = -1,
        CancellationToken ct = default)
    {
        var gate = await GateAsync(pageId, mid, ct);
        if (gate is not null) { return gate; }

        ItemId = itemId;

        if (itemId > -1)
        {
            var linkItem = await _links.GetLinkAsync(itemId, ct);
            // Source checks ModuleId on the loaded-or-blank entity: a missing
            // item (ModuleId -1) fails the check just like a mismatched one.
            if ((linkItem?.ModuleId ?? -1) != ModuleId)
            {
                return Redirect("/AccessDenied.aspx");
            }

            if (linkItem is not null)
            {
                Title = linkItem.Title;
                Description = linkItem.Description;
                UrlText = linkItem.Url;
                ViewOrderText = linkItem.ViewOrder.ToString();
                UseNewWindow = linkItem.Target == "_blank";
                if (linkItem.CreatedByUser > 0)
                {
                    var creator = await _users.GetByIdAsync(linkItem.CreatedByUser, ct);
                    CreatedByUserName = creator?.LoginName;
                }
            }
        }
        else
        {
            // new item: txtViewOrder Text="500" from the .aspx markup
            ViewOrderText = "500";
        }

        var referrer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrEmpty(referrer) && HdnReturnUrl.Length == 0)
        {
            HdnReturnUrl = referrer;
            CancelUrl = referrer;
        }

        if (string.IsNullOrEmpty(CancelUrl))
        {
            CancelUrl = MojoContext.GetCurrentPageUrl(_currentPage);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int pageId, int mid, int itemId = -1,
        CancellationToken ct = default)
    {
        var gate = await GateAsync(pageId, mid, ct);
        if (gate is not null) { return gate; }

        ItemId = itemId;

        // EncodeTextboxControls on POST: single-line inputs -> RemoveMarkup,
        // textarea -> SanitizeMarkup for non-admin users.
        Title = MarkupHelper.RemoveMarkup(Title ?? string.Empty);
        UrlText = MarkupHelper.RemoveMarkup(UrlText ?? string.Empty);
        ViewOrderText = MarkupHelper.RemoveMarkup(ViewOrderText ?? string.Empty);
        Description = WebUser.IsAdmin(_user)
            ? Description
            : MarkupHelper.SanitizeMarkup(Description);

        // RequiredFieldValidator equivalents (reqUrl disabled when DescriptionOnly).
        if (string.IsNullOrWhiteSpace(Title))
        {
            Errors.Add("You Must Enter a Valid Title");
        }

        if (!DescriptionOnly && string.IsNullOrWhiteSpace(UrlText))
        {
            Errors.Add("You Must Enter a Valid URL");
        }

        if (string.IsNullOrWhiteSpace(ViewOrderText)
            || !int.TryParse(ViewOrderText, out int viewOrder))
        {
            Errors.Add("You Must Enter a Valid View Order");
        }

        if (Errors.Count > 0)
        {
            CancelUrl = MojoContext.GetCurrentPageUrl(_currentPage);
            return Page();
        }

        var linkItem = await _links.GetLinkAsync(itemId, ct) ?? new Link();
        if (linkItem.ItemId > -1 && linkItem.ModuleId != ModuleId)
        {
            return Redirect("/AccessDenied.aspx");
        }

        var module = await _modules.GetByIdAsync(ModuleId, ct);
        linkItem.ModuleGuid = module?.ModuleGuid ?? Guid.Empty;
        linkItem.ModuleId = ModuleId;
        linkItem.Title = Title!;
        linkItem.Target = UseNewWindow ? "_blank" : string.Empty;
        linkItem.Description = Description ?? string.Empty;
        linkItem.Url = ResolvePostedUrl();
        linkItem.ViewOrder = int.Parse(ViewOrderText!);

        var linkUser = await _users.GetByIdAsync(_user.UserId, ct);
        if (linkUser is not null)
        {
            linkItem.CreatedByUser = linkUser.UserId;
            linkItem.UserGuid = linkUser.UserGuid;
        }

        await _linkService.SaveAsync(linkItem, ct);
        if (PageId > 0)
        {
            await _pages.UpdateLastModifiedAsync(PageId, DateTime.UtcNow, ct);
        }

        if (HdnReturnUrl.Length > 0)
        {
            return Redirect(HdnReturnUrl);
        }

        return Redirect(MojoContext.GetCurrentPageUrl(_currentPage));
    }

    public async Task<IActionResult> OnPostDeleteAsync(int pageId, int mid, int itemId = -1,
        CancellationToken ct = default)
    {
        var gate = await GateAsync(pageId, mid, ct);
        if (gate is not null) { return gate; }

        ItemId = itemId;

        if (itemId != -1)
        {
            var link = await _links.GetLinkAsync(itemId, ct);
            if ((link?.ModuleId ?? -1) != ModuleId)
            {
                return Redirect("/AccessDenied.aspx");
            }

            if (link is not null)
            {
                await _linkService.DeleteAsync(link.ItemId, ct);
                if (pageId > 0)
                {
                    await _pages.UpdateLastModifiedAsync(pageId, DateTime.UtcNow, ct);
                }
            }
        }

        if (HdnReturnUrl.Length > 0)
        {
            return Redirect(HdnReturnUrl);
        }

        return Redirect(MojoContext.GetCurrentPageUrl(_currentPage));
    }

    private string ResolvePostedUrl()
    {
        var raw = UrlText ?? string.Empty;
        if (!UseProtocolDropdown || raw.StartsWith("/"))
        {
            return raw;
        }

        var protocol = string.IsNullOrEmpty(Protocol) ? "http://" : Protocol;
        return protocol + raw.Replace("https://", string.Empty)
                             .Replace("http://", string.Empty)
                             .Replace("~/", string.Empty);
    }
}
