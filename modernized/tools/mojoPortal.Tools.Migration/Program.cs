using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Driver;
using mojoPortal.Business;
using mojoPortal.Data.Mongo;

// mojoPortal legacy->MongoDB migration tool.
//   import   <fixture.json> --cs <conn> --db <name>  load a legacy-shaped export
//   reconcile <fixture.json> --cs <conn> --db <name> compare fixture vs database
// Exit codes: 0 success, 1 usage/IO error, 2 reconciliation mismatches.

if (args.Length < 1)
{
    return Usage();
}

var cmd = args[0];
string? fixturePath = null;
var conn = "mongodb://localhost:27017";
var db = "mojoportal";

for (int i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--cs": conn = args[++i]; break;
        case "--db": db = args[++i]; break;
        default:
            if (fixturePath is null) { fixturePath = args[i]; }
            break;
    }
}

if (fixturePath is null || !File.Exists(fixturePath))
{
    Console.Error.WriteLine($"fixture file required: {fixturePath}");
    return Usage();
}

var json = await File.ReadAllTextAsync(fixturePath);
var fixture = JsonSerializer.Deserialize<LegacyExport>(json, Json.Options)
    ?? throw new InvalidDataException("empty fixture");

var store = new MongoStore(new MongoOptions { ConnectionString = conn, Database = db });

return cmd switch
{
    "import" => await ImportAsync(store, fixture),
    "reconcile" => await ReconcileAsync(store, fixture),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: mojoPortal.Tools.Migration <import|reconcile> <fixture.json> [--cs conn] [--db name]");
    return 1;
}

static async Task<int> ImportAsync(MongoStore store, LegacyExport f)
{
    await store.EnsureIndexesAsync();

    await InsertAll(store.Sites, f.Sites, "mp_Sites");
    await InsertAll(store.Pages, f.Pages, "mp_Pages");
    await InsertAll(store.FriendlyUrls, f.FriendlyUrls, "mp_FriendlyUrls");
    await InsertAll(store.ModuleDefinitions, f.ModuleDefinitions, "mp_ModuleDefinitions");
    await InsertAll(store.Modules, f.Modules, "mp_Modules");
    await InsertAll(store.PageModules, f.PageModules, "mp_PageModules");
    await InsertAll(store.ModuleSettings, f.ModuleSettings, "mp_ModuleSettings");
    await InsertAll(store.Links, f.Links, "mp_Links");
    await InsertAll(store.Roles, f.Roles, "mp_Roles");
    await InsertAll(store.Users, f.Users, "mp_Users");
    await InsertAll(store.UserRoles, f.UserRoles, "mp_UserRoles");

    // Re-seed each counter so issued ids never collide with imported rows.
    // CounterService increments seq then issues seed + seq - 1, so storing
    // seq = maxExisting - seed + 1 makes the next issued id maxExisting + 1.
    await ReseedAsync(store, "mp_Links", seed: 0, f.Links.Select(l => l.ItemId).DefaultIfEmpty(-1).Max());
    await ReseedAsync(store, "mp_Modules", seed: 0, f.Modules.Select(m => m.ModuleId).DefaultIfEmpty(-1).Max());
    await ReseedAsync(store, "mp_ModuleSettings", seed: 1, f.ModuleSettings.Select(s => s.Id).DefaultIfEmpty(0).Max());

    Console.WriteLine("import complete");
    return 0;
}

static async Task InsertAll<T>(IMongoCollection<T> coll, List<T> rows, string name)
{
    if (rows.Count == 0)
    {
        Console.WriteLine($"{name}: 0 rows");
        return;
    }

    await coll.InsertManyAsync(rows, new InsertManyOptions { IsOrdered = true });
    Console.WriteLine($"{name}: {rows.Count} rows");
}

static async Task ReseedAsync(MongoStore store, string name, int seed, int maxExisting)
{
    var nextSeq = maxExisting - seed + 1;
    var update = new MongoDB.Bson.BsonDocument("$set",
        new MongoDB.Bson.BsonDocument("seq", nextSeq));
    await store.Counters.UpdateOneAsync(
        Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", name),
        update,
        new UpdateOptions { IsUpsert = true });
    Console.WriteLine($"counter {name}: next id {maxExisting + 1}");
}

static async Task<int> ReconcileAsync(MongoStore store, LegacyExport f)
{
    var mismatches = 0;

    mismatches += await CheckCount(store.Sites, f.Sites.Count, "mp_Sites");
    mismatches += await CheckCount(store.Pages, f.Pages.Count, "mp_Pages");
    mismatches += await CheckCount(store.FriendlyUrls, f.FriendlyUrls.Count, "mp_FriendlyUrls");
    mismatches += await CheckCount(store.ModuleDefinitions, f.ModuleDefinitions.Count, "mp_ModuleDefinitions");
    mismatches += await CheckCount(store.Modules, f.Modules.Count, "mp_Modules");
    mismatches += await CheckCount(store.PageModules, f.PageModules.Count, "mp_PageModules");
    mismatches += await CheckCount(store.ModuleSettings, f.ModuleSettings.Count, "mp_ModuleSettings");
    mismatches += await CheckCount(store.Links, f.Links.Count, "mp_Links");
    mismatches += await CheckCount(store.Roles, f.Roles.Count, "mp_Roles");
    mismatches += await CheckCount(store.Users, f.Users.Count, "mp_Users");
    mismatches += await CheckCount(store.UserRoles, f.UserRoles.Count, "mp_UserRoles");

    // Field-level spot checks for the Links slice.
    foreach (var expected in f.Links)
    {
        var actual = await store.Links.Find(l => l.ItemId == expected.ItemId).FirstOrDefaultAsync();
        if (actual is null)
        {
            Console.WriteLine($"mp_Links[{expected.ItemId}]: MISSING");
            mismatches++;
            continue;
        }

        foreach (var (field, e, a) in new (string, object?, object?)[]
        {
            ("ModuleId", expected.ModuleId, actual.ModuleId),
            ("Title", expected.Title, actual.Title),
            ("Url", expected.Url, actual.Url),
            ("Target", expected.Target, actual.Target),
            ("ViewOrder", expected.ViewOrder, actual.ViewOrder),
            ("Description", expected.Description, actual.Description),
            ("ItemGuid", expected.ItemGuid, actual.ItemGuid),
        })
        {
            if (!Equals(e, a))
            {
                Console.WriteLine($"mp_Links[{expected.ItemId}].{field}: expected '{e}' got '{a}'");
                mismatches++;
            }
        }
    }

    Console.WriteLine(mismatches == 0 ? "reconcile: OK (0 mismatches)" : $"reconcile: {mismatches} mismatch(es)");
    return mismatches == 0 ? 0 : 2;
}

static async Task<int> CheckCount<T>(IMongoCollection<T> coll, int expected, string name)
{
    var actual = await coll.EstimatedDocumentCountAsync();
    Console.WriteLine($"{name}: expected {expected}, found {actual}");
    return actual == expected ? 0 : 1;
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

// Legacy export shape: one JSON array per mp_* table, property names match the
// entity field names (which preserve the column names).
public sealed class LegacyExport
{
    public List<Site> Sites { get; set; } = new();
    public List<SitePage> Pages { get; set; } = new();
    public List<FriendlyUrl> FriendlyUrls { get; set; } = new();
    public List<ModuleDefinition> ModuleDefinitions { get; set; } = new();
    public List<Module> Modules { get; set; } = new();
    public List<PageModule> PageModules { get; set; } = new();
    public List<ModuleSetting> ModuleSettings { get; set; } = new();
    public List<Link> Links { get; set; } = new();
    public List<Role> Roles { get; set; } = new();
    public List<SiteUser> Users { get; set; } = new();
    public List<UserRole> UserRoles { get; set; } = new();
}
