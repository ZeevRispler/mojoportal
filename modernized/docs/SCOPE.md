# Modernization Scope — mojoportal → ASP.NET Core + MongoDB

Run ID: `mojoportal-2026-10-02-attempt-01`
Source: `https://github.com/i7MEDIA/mojoportal` pinned at commit `2205a743704967bf561e94eeea9f02459e94d372`.

## Chosen slice

**The "Links" (List) content feature end-to-end, plus the minimum CMS host context it
depends on.** This slice was chosen from source discovery because it is a complete,
user-facing vertical: anonymous visitors render a list of links on a CMS page,
authorized editors manage items through the admin UI, and the feature exercises
ordering, paging, module settings, validation, role-based authorization, tenant
scoping, and cascade delete semantics — all behaviors a modernization must preserve.

### In scope (implemented)

| Area | Source of truth | Behavior preserved |
|---|---|---|
| Link entity & persistence | `mojoPortal.Features.Business/LinkModule/Links.cs`, `mojoPortal.Features.Data.MSSQL/LinkModule/DBLinks.cs`, `Setup/applications/linksmodule/SchemaInstallScripts/mssql/0.0.0.7.config` + upgrades through `1.0.0.9` | int `ItemID` (IDENTITY(0,1)), `ModuleID`, `Title` (255), `Url` (nvarchar(max), NOT NULL), `Target` (20, default `_blank`), `ViewOrder` (default 500), `Description`, `CreatedDate`, `CreatedBy`, `ItemGuid`, `ModuleGuid`, `UserGuid`; `Save()` = Create when `ItemID == -1` else Update; `ItemGuid` assigned on create |
| Ordering & paging | `mp_Links_Select`, `mp_Links_SelectPage`, `mp_Links_GetCount`, `DBLinks.GetPage` | `ORDER BY ViewOrder, Title`; exact `totalPages` arithmetic (incl. pageSize<=0 edge cases); index-bound window `IndexID > lower && IndexID < upper` |
| Public rendering | `mojoPortal.Features.UI/List/Controls/ItemList.ascx(.cs)`, `ListModule.ascx.cs`, `ListConfiguration.cs`, `ListDisplaySettings.cs` | `<div class="listintro">` + `<ul class="linkitem">` with `linkitem`/`linkaltitem` li classes, `linkdesc` divs below link (DescriptionAboveLink default false), `mojolink` anchors, `target='_blank'` only when Target=="_blank", `title` attr = HtmlEncode(title), raw unencoded anchor text, `~/` and `/` URL resolution, empty Url → no anchor, DescriptionOnly mode (`linkdesc`/`linkdescalt`), IsEditable edit links, InstanceCssClass |
| Paged view page | `mojoPortal.Features.UI/List/ViewList.aspx.cs` | `/List/ViewList.aspx?pageid&mid&pagenumber`, `UserCanViewPage` gate → AccessDenied, module title + Add Link + Edit Intro links for editors, pager visible only when `totalPages > 1`, `rel=canonical`/`next`/`prev` head links, `listviewlist` body class |
| Edit page | `mojoPortal.Features.UI/List/Edit.aspx.cs` | `/List/Edit.aspx?ItemID&mid&pageid`; unauthenticated → login redirect; `UserCanEditModule` gate → AccessDenied; `link.ModuleId != mid` → AccessDenied; Title required, Url required (disabled when DescriptionOnly), ViewOrder int required; Target = "_blank" iff checkbox else empty; protocol-dropdown URL join rule; `CreatedBy`/`UserGuid` stamped from current user on save; delete only for existing items; return-url redirect; `listedit` body class |
| Edit intro | `mojoPortal.Features.UI/List/EditIntro.aspx.cs` | `/List/EditIntro.aspx?pageid&mid`; same auth gates; `ModuleSettings.UpdateModuleSetting(..., "IntroContent", value)`; `listeditintro` body class |
| Module settings | `ListConfiguration.cs`, `FeatureDefinitions/70000_LinksModule.config` | `IntroContent`, `LinksShowDescriptionSetting`(false), `LinksShowOnlyDescriptionSetting`(false, implies UseDescription), `LinksEnablePagingSetting`(false), `UseAjaxPaging`(true), `LinksPageSizeSetting`(50), `LinksExtraCssClassSetting`, `ShowPageLeftContentSetting`/`ShowPageRightContentSetting`(class defaults true) |
| Authorization matrix | `Web/Components/mojoBasePage.cs` (`UserCanViewPage`, `UserCanEditModule`), `mojoPortal.Business.WebHelpers/WebUser.cs` (`IsInRole`, `IsInRoles`, `IsAdmin`, `IsContentAdmin`), `SiteUtils.UserIsSiteEditor` | Exact decision order incl. Admins override, `"All Users;"` semantics, `"Admins;"` special-casing for ContentAdmin/SiteEditor, module-on-page + feature-Guid matching, `IsGlobal` module rule, `EditUserId` grant, second loop matching `ModuleId` without featureGuid (source quirk preserved), related-sites `SiteRootEditRoles` for site editors (off by default) |
| CMS host context | `mp_Pages`, `mp_FriendlyUrls`, `mp_Modules`, `mp_PageModules`, `mp_ModuleDefinitions`, `mp_ModuleSettings`, `mp_Sites`, `mp_Users`, `mp_Roles`, `mp_UserRoles` schema | Friendly-URL → page resolution, `mp_Modules_SelectByPage` publish-window filter (`PublishBeginDate <= UtcNow && (PublishEndDate IS NULL OR > UtcNow)`) ordered by `ModuleOrder`, module placement per pane |
| Cascades | `FK_Links_Modules ON DELETE CASCADE`, `mp_Links_DeleteByModule`, `mp_Links_DeleteBySite` | Deleting a module deletes its links; deleting a site deletes links of all its modules |
| Auth | `mp_Users`/`mp_Roles`/`mp_UserRoles`, Forms authentication | Cookie sign-in with role claims; login page `/Secure/Login.aspx?returnurl=` |

### Out of scope (remaining whole-product denominator)

Every other shipped feature and CMS subsystem remains unported: Html content module,
Blog, Forums, Contact Form, Event Calendar, Feed Manager, Image Gallery,
KDMediaPlayer, Poll, Shared Files, Survey, site search/Lucene indexing
(`IIndexableContent` event wiring is a no-op hook here), newsletters, commerce,
content workflow/drafts (`DraftEditRoles`, `DraftApprovalRoles` are stored but not
acted on), skin/theme system, site admin UI, page/module management UI, membership
management (registration, password reset/retrieval, lockout counters,
question/answer), related-sites mode, SSL enforcement, browser-cache headers,
`IsFishyPost` CSRF heuristics (replaced by ASP.NET Core antiforgery), AJAX
UpdatePanel paging (`UseAjaxPaging` setting is honored as "no full-page pager" →
paging links are still rendered as plain GET links), WebSnapr, protocol dropdown
(`List:UseProtocolDropdown`, honored when configured).

## Low-risk technical assumptions (documented, proceeded without asking)

1. **Integer IDs preserved** via a MongoDB `counters` collection
   (`findOneAndUpdate $inc`, atomic) so that URL shapes (`ItemID`, `mid`, `pageid`)
   and ordering semantics match the source exactly. Seeds match T-SQL IDENTITY
   (links `ItemID` from 0, modules `ModuleID` from 0, pages/users/roles from 1).
2. **SQL default collation `CI`** for `Title` ordering is reproduced with a
   MongoDB collation `{ locale: 'en', strength: 2 }` on list/page sorts.
3. **`datetime` semantics**: source passes `DateTime.UtcNow` for publish windows
   and stores server-local `DateTime.Now` for `CreatedDate`. We store UTC in
   MongoDB and stamp `CreatedDate = DateTime.Now` on the app tier to match.
4. **Password storage** uses PBKDF2-SHA256 (100k iterations, per-user salt).
   Legacy membership hashing/lockout/registration flows are not part of the slice;
   synthetic users only.
5. **Single site**: host context resolves the first site by `SiteID` (mojoportal's
   default single-site mode). Multi-site host-header routing is out of scope.
6. **Antiforgery tokens** replace `SiteUtils.IsFishyPost` (same intent: reject
   cross-site POSTs on edit pages).
7. **UpdatePanel/AJAX paging** is replaced by equivalent server-rendered GET
   links; `UseAjaxPaging` only affects whether `pagenumber` is read from the query
   string in the source too (non-ajax path), so behavior converges.

## Public acceptance criteria

1. `dotnet build` of `modernized/mojoPortal.Modernized.sln` succeeds on .NET 8.
2. App starts against a real MongoDB (docker `mongo:7.0`) and serves:
   - `GET /{friendlyUrl}` renders the CMS page with its Links modules in
     `ModuleOrder`, publish-window filtered.
   - `GET /List/ViewList.aspx?pageid=&mid=&pagenumber=` renders the paged list
     with ordering `ViewOrder, Title` and `totalPages` math identical to source.
   - Unauthorized/non-role users → `/AccessDenied.aspx`; unauthenticated POST/edit
     → `/Secure/Login.aspx?ReturnUrl=`.
   - `GET|POST /List/Edit.aspx`, `/List/EditIntro.aspx` perform the full
     create/update/delete flows with the source validation rules.
   - `POST /Secure/Login.aspx` authenticates synthetic users and applies the
     full role matrix.
3. `dotnet test` runs unit + real-MongoDB integration + end-to-end HTTP tests
   (Testcontainers) and all pass.
4. `tools/mojoPortal.Tools.Migration` imports a synthetic fixture into MongoDB
   and `verify` produces a reconciliation report (counts + item-level diff).
5. Rollback: `modernized` is a pure addition; no legacy file is modified or
   deleted. Dropping the `mojoportal` MongoDB database returns to zero state.

## Behavior derived from source, not live parity

The legacy WebForms app requires Windows/IIS/SQL Server and was not executed in
this session. All assertions are **source-derived** (transcribed from the pinned
commit's C# and T-SQL) and labeled as such; live legacy parity was not verified.
