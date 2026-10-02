# Evidence log — mojoportal-2026-10-02-attempt-01

Environment: Ubuntu (Devin cloud VM), .NET SDK 8.0.425 (`/home/ubuntu/.dotnet`),
Docker (mongo:7.0 image), x86_64. Timestamps UTC, 2026-10-02.

Source pinned: `git rev-parse HEAD` → `2205a743704967bf561e94eeea9f02459e94d372`
(see `source-revision.txt`). Legacy tree unmodified — `git status` shows only
untracked `modernized/`.

## Commands & exit codes

| # | Command | Exit | Result |
|---|---|---|---|
| 1 | `git clone https://github.com/i7MEDIA/mojoportal` | 0 | source at pinned commit |
| 2 | `dotnet restore` (modernized/) | 0 | MongoDB.Driver 3.5.0, HtmlSanitizer 9.2.1039, xunit 2.9.3, Testcontainers.MongoDb 4.8.0, Mvc.Testing 8.0.31, NET.Test.Sdk 17.14.1 |
| 3 | `dotnet build` | 0 | 0 warnings, 0 errors (5 projects) |
| 4 | `dotnet test tests/mojoPortal.Tests` | 0 | **86 passed / 0 failed** (duration ~9s) |
| 5 | `dotnet run --project tools/mojoPortal.Tools.Migration -- import tools/fixtures/legacy-export.json --cs mongodb://localhost:27019 --db migdemo` | 0 | 11 collections imported, counters re-seeded (next ids 2/1/2) |
| 6 | `dotnet run --project tools/mojoPortal.Tools.Migration -- reconcile ... --db migdemo` | 0 | `reconcile: OK (0 mismatches)` |
| 7 | `dotnet run --project src/mojoPortal.Web` + `curl /home` | — | HTTP 200; rendered `Featured Links` module with `mojolink` anchors, Beta (ViewOrder 100) before Alpha (500), `~/docs/page`→`/docs/page`, `target='_blank'` |

## Per-unit status

| Unit | Status |
|---|---|
| Build / startup | complete — 0 errors; app boots and serves `/home` (verified via curl + E2E) |
| MongoDB persistence | complete — real mongo:7.0 (container), GuidSerializer(Standard), indexes via EnsureIndexesAsync |
| Links CRUD + ordering/paging | complete — source `ORDER BY ViewOrder, Title` via en/Secondary collation; index-bound window + totalPages arithmetic incl. pageSize=0 DivRem throw |
| Authorization matrix | complete — ported decision order incl. moduleFoundOnPage gate, `Admins;` special-cases, `EditUserId`, related-sites off by default |
| Public rendering (ItemList) | complete — `mojolink` markup verified byte-equal incl. spacing; `~/`,`/` URL rules; raw anchor text + HtmlEncoded title attr |
| Edit/EditIntro/ViewList pages | complete — gates (login → AccessDenied → module-mismatch), validation messages, protocol-dropdown URL rule, Target=_blank iff checkbox, return-url |
| Login/auth | complete — ordered gates, PasswordFormat 0/1 paths, failed-attempt window, cookie sign-in |
| Module settings upsert | complete — (ModuleId,SettingName) unique upsert, preserves SettingGuid |
| Cascades (module/site delete) | complete — repository-level (no DB cascade) |
| Migration import + reconcile | complete — insert-only + count/field checks, exit codes 0/2 |
| Indexes | complete — see docs/MIGRATION.md table |
| Rollback | complete — additive-only `modernized/`; legacy untouched; drop-db + remove dir |
| Whole-product parity | NOT CLAIMED — Links slice only; live legacy runtime comparison not performed (source-derived assertions labeled in tests) |

## Failed-attempt audit (significant)

| Attempt | Failure | Resolution |
|---|---|---|
| Guid serialization w/o representation | `BsonSerializationException` on all Guid writes (driver 3.x) | explicit `GuidSerializer(GuidRepresentation.Standard)` registration |
| Mongo config read eagerly in `Program.cs` | E2E override unreachable → localhost:27017 refusals | lazy binding via DI factory lambda |
| `Verify(password, salt, hash)` argument order | all hashed logins failed | corrected to `Verify(password, hash, salt)`; covered by E2E login test |
| Test seeds missing `UserGuid` | failed-password increments skipped (source gates on `UserGuid != Empty`) | fixed seeds, not the port |
| HtmlSanitizer 8.2.0 / AngleSharp 1.3.0 pins | NU1605/advisory conflicts | pinned HtmlSanitizer 9.2.1039; regex-based antiforgery token extraction |
| Reseed formula off-by-one | next issued id skipped a value | `seq = maxExisting - seed + 1` (verified via import+counter check) |
| Fixture `FeatureGuid` key vs `Guid` property | module definition guid empty → no item list rendered | fixture key corrected; smoke re-verified |
| Test expectation vs source (several) | e.g. IDENTITY(0,1) first id, featureGuid quirk unreachable behind moduleFoundOnPage gate, link-markup spacing, `Math.DivRem` throw | source re-verified; expectations corrected — implementation never weakened |

## Notes

- No production systems or data accessed; all fixtures synthetic.
- Ajax UpdatePanel paging is replaced by full postbacks to `ViewList.aspx`
  (documented deviation in SCOPE.md).
- Self-tests are the implementation's own evidence — not independent grading.
