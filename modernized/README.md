# mojoportal → ASP.NET Core + MongoDB (Links slice)

Modernization of the "Links" (List) content feature of
[i7MEDIA/mojoportal](https://github.com/i7MEDIA/mojoportal) @
`2205a743704967bf561e94eeea9f02459e94d372`: WebForms + T-SQL → ASP.NET Core 8
Razor Pages + MongoDB Driver 3.x.

## Layout

| Path | Contents |
|---|---|
| `src/mojoPortal.Business` | Entities, paging math, `ListConfiguration`, authorization matrix (`UserCanViewPage`/`UserCanEditModule`/role helpers), link service, HTML rendering helpers, markup sanitizer |
| `src/mojoPortal.Data.Mongo` | `MongoStore` (collections, class maps, indexes), repositories implementing the legacy stored-proc contracts, `CounterService` (IDENTITY→counter) |
| `src/mojoPortal.Web` | Razor Pages host: friendly-URL CMS page (`/home`, `/Default.aspx?pageid=`), `List/ViewList.aspx`, `List/Edit.aspx`, `List/EditIntro.aspx`, `Secure/Login.aspx`, `AccessDenied.aspx`, cookie auth |
| `tools/mojoPortal.Tools.Migration` | `import` / `reconcile` CLI for legacy→Mongo data moves |
| `tools/fixtures/legacy-export.json` | Synthetic legacy export (no real user data) |
| `tests/mojoPortal.Tests` | 86 tests: unit (paging/auth/config/markup/link-html), Mongo integration (Testcontainers), E2E (`WebApplicationFactory` + real MongoDB) |
| `docs/SCOPE.md` | Slice scope, behavioral contract, remaining modules |
| `docs/MIGRATION.md` | Setup, indexes, migration flow, rollback |
| `evidence/` | Pinned revision, run logs |

## Quickstart

```bash
docker run -d --name mongo -p 27017:27017 mongo:7.0
dotnet build
dotnet test tests/mojoPortal.Tests      # 86 tests incl. real-MongoDB integration+E2E
dotnet run --project tools/mojoPortal.Tools.Migration -- import tools/fixtures/legacy-export.json
dotnet run --project src/mojoPortal.Web # http://localhost:5000/home
```

See `docs/MIGRATION.md` for the full reproducible-setup and rollback notes.
