using Microsoft.AspNetCore.Authentication.Cookies;
using mojoPortal.Business;
using mojoPortal.Business.Authorization;
using mojoPortal.Data.Mongo;
using mojoPortal.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Secure/Login.aspx";
        options.AccessDeniedPath = "/AccessDenied.aspx";
        options.Cookie.Name = ".MOJOAUTH";
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton(sp
    => builder.Configuration.GetSection("Mongo").Get<MongoOptions>() ?? new MongoOptions());
builder.Services.AddSingleton<MongoStore>();
builder.Services.AddSingleton<ICounterRepository, CounterService>();
builder.Services.AddSingleton<ISiteRepository, SiteRepository>();
builder.Services.AddSingleton<IPageRepository, PageRepository>();
builder.Services.AddSingleton<IModuleRepository, ModuleRepository>();
builder.Services.AddSingleton<IModuleSettingsRepository, ModuleSettingsRepository>();
builder.Services.AddSingleton<ILinkRepository, LinkRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<LinkService>();
builder.Services.AddSingleton<MojoPasswordHasher>();
builder.Services.AddSingleton(_ => new PermissionService(
    relatedSitesEnabled: builder.Configuration.GetValue("Site:RelatedSitesEnabled", false),
    siteRootEditRoles: builder.Configuration.GetValue<string>("Site:SiteRootEditRoles") ?? string.Empty));
builder.Services.AddScoped<IMojoUser, ClaimsMojoUser>();
builder.Services.AddScoped<MojoContext>();
builder.Services.AddSingleton<ItemListBuilder>();

var app = builder.Build();

var store = app.Services.GetRequiredService<MongoStore>();
await store.EnsureIndexesAsync();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
// CMS page host: anything that didn't match a literal route resolves as a
// friendly page URL (~/home -> /home), like the legacy URL rewriter.
app.MapFallbackToPage("/CmsPage");

app.Run();

public partial class Program { }
