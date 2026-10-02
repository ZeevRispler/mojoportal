using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using mojoPortal.Business;
using mojoPortal.Data.Mongo;
using mojoPortal.Tests.Integration;
using Xunit;

namespace mojoPortal.Tests.E2E;

/// <summary>
/// End-to-end against the real app on a real MongoDB: page view, module
/// visibility, auth gates, full login flow (antiforgery + cookie), edit/save
/// round-trip, and settings write-through.
/// </summary>
[Collection("mongo")]
public sealed class WebAppTests
{
    private readonly MongoFixture _mongo;
    public WebAppTests(MongoFixture mongo) => _mongo = mongo;

    private sealed class App : WebApplicationFactory<Program>
    {
        public required MongoStore Store;
        public required string ConnectionString;
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Mongo:ConnectionString"] = ConnectionString,
                    ["Mongo:Database"] = Store.Db.DatabaseNamespace.DatabaseName,
                    ["List:UseProtocolDropdown"] = "true",
                });
            });
        }
    }

    private App NewApp(string db)
    {
        var store = _mongo.NewStore(db);
        var app = new App { Store = store, ConnectionString = _mongo.ConnectionString };
        // Force host construction now so index creation happens once.
        _ = app.Services;
        return app;
    }

    private static async Task SeedSiteAsync(MongoStore store)
    {
        await store.EnsureIndexesAsync();
        var counters = new CounterService(store);
        var modules = new ModuleRepository(store, counters);

        await store.Sites.InsertOneAsync(new Site
        {
            SiteId = 1, SiteGuid = Guid.NewGuid(), SiteName = "TestSite",
            UseEmailForLogin = true, MaxInvalidPasswordAttempts = 5,
            PasswordAttemptWindowMinutes = 30,
        });
        await store.Pages.InsertManyAsync(
        [
            new SitePage
            {
                PageId = 1, SiteId = 1, PageName = "Home", PageTitle = "Home",
                Url = "~/home", PageOrder = 0, AuthorizedRoles = "All Users;",
                EditRoles = "", SiteGuid = Guid.Empty,
            },
        ]);
    }

    private static async Task<int> SeedLinksModuleAsync(MongoStore store, int pageId, int siteId, string pane = "contentpane")
    {
        var counters = new CounterService(store);
        var repo = new ModuleRepository(store, counters);
        var m = new Module
        {
            SiteId = siteId, ModuleTitle = "My Links", FeatureGuid = Link.FeatureGuid,
            ViewRoles = "All Users;", ModuleGuid = Guid.NewGuid(),
        };
        m.ModuleId = await repo.CreateAsync(m);
        await store.PageModules.InsertOneAsync(new PageModule
        {
            PageId = pageId, ModuleId = m.ModuleId, PaneName = pane,
        });
        return m.ModuleId;
    }

    private static string AntiforgeryToken(string html)
        => Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    private static string LocationPath(HttpResponseMessage resp)
        => resp.Headers.Location is null ? string.Empty
            : (resp.Headers.Location.IsAbsoluteUri ? resp.Headers.Location.AbsolutePath : resp.Headers.Location.ToString());

    private static HttpClient Client(App app)
        => app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    [Fact]
    public async Task Anonymous_views_page_and_links_in_order()
    {
        var app = NewApp("e2e_view");
        await SeedSiteAsync(app.Store);
        int mid = await SeedLinksModuleAsync(app.Store, pageId: 1, siteId: 1);

        var links = new LinkRepository(app.Store, new CounterService(app.Store));
        await links.AddLinkAsync(new Link { ModuleId = mid, Title = "Beta", Url = "https://b", ViewOrder = 1 });
        await links.AddLinkAsync(new Link { ModuleId = mid, Title = "alpha", Url = "https://a", ViewOrder = 2 });

        var resp = await Client(app).GetAsync("/home");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("My Links", html);
        Assert.True(html.IndexOf("Beta", StringComparison.Ordinal) < html.IndexOf("alpha", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unknown_path_404s_and_unknown_pageid_redirects_denied()
    {
        var app = NewApp("e2e_404");
        await SeedSiteAsync(app.Store);
        var client = Client(app);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/nonexistent")).StatusCode);

        var resp = await client.GetAsync("/List/ViewList.aspx?pageid=99&mid=1&pagenumber=1");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/AccessDenied.aspx", LocationPath(resp));
    }

    [Fact]
    public async Task Edit_page_requires_auth_and_editor_rights()
    {
        var app = NewApp("e2e_gate");
        await SeedSiteAsync(app.Store);
        int mid = await SeedLinksModuleAsync(app.Store, 1, 1);
        var client = Client(app);

        // Unauthenticated -> challenge to login page.
        var resp = await client.GetAsync($"/List/Edit.aspx?pageid=1&mid={mid}&itemid=-1");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/Secure/Login.aspx", LocationPath(resp));
    }

    [Fact]
    public async Task Full_login_and_edit_roundtrip()
    {
        var app = NewApp("e2e_login");
        await SeedSiteAsync(app.Store);
        int mid = await SeedLinksModuleAsync(app.Store, 1, 1);
        var client = Client(app);

        var hasher = new mojoPortal.Web.Services.MojoPasswordHasher();
        var (hash, salt) = hasher.Hash("pw123!");
        await app.Store.Users.InsertOneAsync(new SiteUser
        {
            UserId = 10, SiteId = 1, Name = "Admin One", LoginName = "admin",
            Email = "a@x", LoweredEmail = "a@x", Pwd = hash, PasswordSalt = salt,
            PasswordFormat = 1, ApprovedForLogin = true, UserGuid = Guid.NewGuid(),
        });
        await app.Store.Roles.InsertOneAsync(new Role { RoleId = 1, SiteId = 1, RoleName = "Admins" });
        await app.Store.UserRoles.InsertOneAsync(new UserRole { UserId = 10, RoleId = 1 });

        // GET login -> antiforgery token + cookie.
        var get = await client.GetAsync("/Secure/Login.aspx?returnurl=/home");
        var token = AntiforgeryToken(await get.Content.ReadAsStringAsync());

        var login = await client.PostAsync("/Secure/Login.aspx?returnurl=/home",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["UserName"] = "a@x",
                ["Password"] = "pw123!",
            }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/home", LocationPath(login));

        // Now authenticated as Admins: edit page loads.
        var edit = await client.GetAsync($"/List/Edit.aspx?pageid=1&mid={mid}&itemid=-1");
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var editHtml = await edit.Content.ReadAsStringAsync();
        var editToken = AntiforgeryToken(editHtml);

        // Save a new link.
        var save = await client.PostAsync($"/List/Edit.aspx?pageid=1&mid={mid}&itemid=-1",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = editToken,
                ["Title"] = "Saved Link",
                ["UrlText"] = "example.com/x",
                ["Protocol"] = "https://",
                ["ViewOrderText"] = "123",
                ["Description"] = "<b>hi</b>",
                ["UseNewWindow"] = "true",
                ["HdnReturnUrl"] = "/home",
            }));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal("/home", LocationPath(save));

        var linkRepo = new LinkRepository(app.Store, new CounterService(app.Store));
        var saved = (await linkRepo.GetLinksAsync(mid)).Single();
        Assert.Equal("Saved Link", saved.Title);
        Assert.Equal("https://example.com/x", saved.Url);   // protocol rule applied
        Assert.Equal("_blank", saved.Target);
        Assert.Equal(123, saved.ViewOrder);
        Assert.Equal("<b>hi</b>", saved.Description);       // admin bypasses sanitize
        Assert.Equal(10, saved.CreatedByUser);

        // Validation failure path: missing title re-renders with the resx message.
        var bad = await client.PostAsync($"/List/Edit.aspx?pageid=1&mid={mid}&itemid=-1",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = editToken,
                ["Title"] = "",
                ["UrlText"] = "x",
                ["ViewOrderText"] = "1",
            }));
        var badHtml = await bad.Content.ReadAsStringAsync();
        Assert.Contains("You Must Enter a Valid Title", badHtml);
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_increments_attempts_and_fails()
    {
        var app = NewApp("e2e_badpw");
        await SeedSiteAsync(app.Store);
        var client = Client(app);

        var hasher = new mojoPortal.Web.Services.MojoPasswordHasher();
        var (hash, salt) = hasher.Hash("right");
        await app.Store.Users.InsertOneAsync(new SiteUser
        {
            UserId = 11, SiteId = 1, LoginName = "u", Email = "u@x", LoweredEmail = "u@x",
            Pwd = hash, PasswordSalt = salt, PasswordFormat = 1, ApprovedForLogin = true,
            UserGuid = Guid.NewGuid(), // source increments only when UserGuid != Empty
        });

        var get = await client.GetAsync("/Secure/Login.aspx");
        var token = AntiforgeryToken(await get.Content.ReadAsStringAsync());
        var resp = await client.PostAsync("/Secure/Login.aspx",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["UserName"] = "u@x",
                ["Password"] = "wrong",
            }));
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Login Failed!", html);

        var user = await app.Store.Users.Find(u => u.UserId == 11).FirstAsync();
        Assert.Equal(1, user.FailedPasswordAttemptCount);
    }

    [Fact]
    public async Task Deleted_user_cannot_login()
    {
        var app = NewApp("e2e_deleted");
        await SeedSiteAsync(app.Store);
        var client = Client(app);
        await app.Store.Users.InsertOneAsync(new SiteUser
        {
            UserId = 12, SiteId = 1, LoginName = "u", Email = "u@x", LoweredEmail = "u@x",
            Pwd = "x", PasswordFormat = 0, ApprovedForLogin = true, IsDeleted = true,
        });

        var get = await client.GetAsync("/Secure/Login.aspx");
        var token = AntiforgeryToken(await get.Content.ReadAsStringAsync());
        var resp = await client.PostAsync("/Secure/Login.aspx",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["UserName"] = "u@x", ["Password"] = "x",
            }));
        Assert.Contains("Login Failed!", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Hidden_from_authenticated_module_disappears_after_login()
    {
        var app = NewApp("e2e_hide");
        await SeedSiteAsync(app.Store);
        int mid = await SeedLinksModuleAsync(app.Store, 1, 1);
        // Flip the module to HideFromAuthenticated.
        await app.Store.Modules.UpdateOneAsync(
            MongoDB.Driver.Builders<Module>.Filter.Eq(m => m.ModuleId, mid),
            MongoDB.Driver.Builders<Module>.Update.Set(m => m.HideFromAuthenticated, true));

        var client = Client(app);
        var anon = await client.GetAsync("/home");
        Assert.Contains("My Links", await anon.Content.ReadAsStringAsync());

        var hasher = new mojoPortal.Web.Services.MojoPasswordHasher();
        var (hash, salt) = hasher.Hash("pw");
        await app.Store.Users.InsertOneAsync(new SiteUser
        {
            UserId = 13, SiteId = 1, LoginName = "u", Email = "u@x", LoweredEmail = "u@x",
            Pwd = hash, PasswordSalt = salt, PasswordFormat = 1, ApprovedForLogin = true,
        });
        var get = await client.GetAsync("/Secure/Login.aspx");
        var token = AntiforgeryToken(await get.Content.ReadAsStringAsync());
        await client.PostAsync("/Secure/Login.aspx",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["UserName"] = "u@x", ["Password"] = "pw",
            }));

        var authed = await client.GetAsync("/home");
        Assert.DoesNotContain("My Links", await authed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EditIntro_writes_module_setting()
    {
        var app = NewApp("e2e_intro");
        await SeedSiteAsync(app.Store);
        int mid = await SeedLinksModuleAsync(app.Store, 1, 1);
        var moduleGuid = (await app.Store.Modules.Find(m => m.ModuleId == mid).FirstAsync()).ModuleGuid;

        var hasher = new mojoPortal.Web.Services.MojoPasswordHasher();
        var (hash, salt) = hasher.Hash("pw");
        await app.Store.Users.InsertOneAsync(new SiteUser
        {
            UserId = 14, SiteId = 1, LoginName = "u", Email = "u@x", LoweredEmail = "u@x",
            Pwd = hash, PasswordSalt = salt, PasswordFormat = 1, ApprovedForLogin = true,
        });
        await app.Store.Roles.InsertOneAsync(new Role { RoleId = 1, SiteId = 1, RoleName = "Admins" });
        await app.Store.UserRoles.InsertOneAsync(new UserRole { UserId = 14, RoleId = 1 });

        var client = Client(app);
        var get = await client.GetAsync("/Secure/Login.aspx");
        var token = AntiforgeryToken(await get.Content.ReadAsStringAsync());
        await client.PostAsync("/Secure/Login.aspx",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["UserName"] = "u@x", ["Password"] = "pw",
            }));

        var page = await client.GetAsync($"/List/EditIntro.aspx?pageid=1&mid={mid}");
        var post = await client.PostAsync($"/List/EditIntro.aspx?pageid=1&mid={mid}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(await page.Content.ReadAsStringAsync()),
                ["IntroContent"] = "<p>intro!</p>",
            }));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        var settings = new ModuleSettingsRepository(app.Store, new CounterService(app.Store));
        var doc = await settings.GetModuleSettingsAsync(mid);
        Assert.Equal("<p>intro!</p>", doc["IntroContent"]);
        var row = await app.Store.ModuleSettings.Find(s => s.ModuleId == mid).FirstAsync();
        Assert.Equal(moduleGuid, row.ModuleGuid);
    }
}
