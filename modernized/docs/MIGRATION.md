# Migration, Reconciliation & Rollback — Links slice

## Reproducible environment

Prereqs: .NET SDK 8.0.x, Docker (for MongoDB and Testcontainers).

```bash
# MongoDB 7 (single node is sufficient; the app uses no transactions)
docker run -d --name mongo -p 27017:27017 mongo:7.0

# restore + build
cd modernized
dotnet restore && dotnet build

# run the test suite (spins an ephemeral mongo:7.0 container per collection fixture)
dotnet test tests/mojoPortal.Tests

# import the synthetic legacy export and reconcile it
dotnet run --project tools/mojoPortal.Tools.Migration -- \
    import tools/fixtures/legacy-export.json --cs mongodb://localhost:27017 --db mojoportal
dotnet run --project tools/mojoPortal.Tools.Migration -- \
    reconcile tools/fixtures/legacy-export.json --cs mongodb://localhost:27017 --db mojoportal

# run the app
dotnet run --project src/mojoPortal.Web
# -> http://localhost:5000/home renders the "Featured Links" module
#    admin login: admin@example.test / admin123 (PasswordFormat=0 plain, synthetic only)
```

Configuration: `Mongo:ConnectionString` / `Mongo:Database` in
`src/mojoPortal.Web/appsettings.json` or `Mongo__*` environment variables.

## Indexes (MongoDB)

Created by `MongoStore.EnsureIndexesAsync()` — idempotent, run at app startup and
by the import tool:

Every collection's `_id` is the legacy PK column (ItemID, ModuleID, UserID, …),
unique by Mongo's `_id` semantics. Secondary indexes:

| Collection | Keys | Options | Legacy source |
|---|---|---|---|
| `mp_Links` | `{ModuleId}` | | `mp_Links_Select` filter |
| `mp_Links` | `{ModuleId, ViewOrder, Title}` | collation `en`, strength Secondary | `ORDER BY ViewOrder, Title` (case-insensitive `Title` sort) |
| `mp_Links` | `{ItemGuid}` | | `ItemGuid uniqueidentifier` lookup |
| `mp_Pages` | `{SiteId, Url}`; `{SiteId, PageOrder}` | | `mp_Pages_SelectBySiteUrl`, menu ordering |
| `mp_FriendlyUrls` | `{SiteId, FriendlyName}` | | `mp_FriendlyUrls` lookup |
| `mp_PageModules` | `{PageId}`; `{ModuleId}` | | `mp_Modules_SelectByPage` join |
| `mp_Modules` | `{SiteId}`; `{ModuleGuid}` | | `mp_Modules_SelectBySite`, guid lookup |
| `mp_ModuleSettings` | `{ModuleId, SettingName}` | unique | `mp_ModuleSettings_Update` upsert key |
| `mp_Users` | `{SiteId, LoginName}`; `{SiteId, LoweredEmail}`; `{UserGuid}` | | `mp_Users_SelectByLoginName`/`ByEmail`/`ByGuid` |
| `mp_UserRoles` | `{UserId}`; `{RoleId}` | | `mp_UserRoles` joins |
| `mp_Roles` | `{SiteId, RoleName}` | | `mp_Roles_SelectBySite` |
| `counters` | `{_id}` | | replaces IDENTITY columns |

## IDENTITY → counters

`counters` documents `{_id: <table>, base: <seed>, seq: n}` back the atomic
`CounterService.NextAsync` (findOneAndUpdate `$inc`). Seeds mirror the T-SQL
IDENTITY seeds: `mp_Links`/`mp_Modules` = 0 (`IDENTITY(0,1)`), `mp_ModuleSettings`
= 1 (`IDENTITY(1,1)`). The import tool re-seeds `seq` to `maxImported - seed + 1`
so the next issued id is `maxImported + 1`.

## Migration flow

1. Export legacy tables to the fixture shape (`tools/fixtures/legacy-export.json`
   documents it — one JSON array per `mp_*` table, keys = entity property names).
2. `import` — `InsertMany` per collection (ordered), creates indexes, re-seeds
   counters. Re-running against the same database fails on duplicate `_id`/`ItemId`
   (idempotent-by-design: wipe or use a fresh `--db` to re-run).
3. `reconcile` — row count per collection plus field-level comparison of every
   imported `mp_Links` row (ModuleId/Title/Url/Target/ViewOrder/Description/ItemGuid).
   Exit code 0 = clean, 2 = mismatches (each printed), 1 = usage/IO error.

## Rollback notes

- All new code lives under `modernized/` inside the repo checkout; the legacy
  WebForms tree is untouched (verified: `git status` shows only untracked
  `modernized/` and no modifications to tracked files).
- To roll back the data side: drop the MongoDB database
  (`mongosh --eval 'db.getSiblingDB("mojoportal").dropDatabase()'`) and remove
  `modernized/`. The legacy SQL schema is unaffected.
- Re-running `import` against a fresh database is the only supported re-run path;
  there is no in-place upgrade from a partial import.

## Known boundaries

- `import` is insert-only; it does not upsert or deduplicate.
- Legacy T-SQL `CASCADE` delete is implemented at the repository layer
  (`DeleteModuleAsync`/`DeleteSiteAsync` also remove links), not by the database.
- See `docs/SCOPE.md` for the full behavioral contract and out-of-scope items.
