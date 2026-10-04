using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using mojoPortal.Business;

namespace mojoPortal.Data.Mongo;

public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "mojoportal";
}

/// <summary>
/// Central MongoDB access point. All mp_* tables map to same-named
/// collections. Integer _ids stand in for T-SQL IDENTITY columns and are
/// issued by the counters collection (atomic findOneAndUpdate $inc).
/// </summary>
public sealed class MongoStore
{
    private static bool _classMapsRegistered;
    private static readonly object _classMapLock = new();

    public MongoClient Client { get; }
    public IMongoDatabase Db { get; }

    public MongoStore(MongoOptions options)
    {
        RegisterClassMaps();
        Client = new MongoClient(options.ConnectionString);
        Db = Client.GetDatabase(options.Database);
    }

    public MongoStore(IMongoClient client, string database)
    {
        RegisterClassMaps();
        Client = (MongoClient)client;
        Db = client.GetDatabase(database);
    }

    private static void RegisterClassMaps()
    {
        lock (_classMapLock)
        {
            if (_classMapsRegistered) { return; }

            var pack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
            };
            ConventionRegistry.Register("mojoConventions", pack, _ => true);

            // Driver v3 requires an explicit Guid representation; Standard
            // (subtype 4) is the interoperable choice.
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            BsonClassMap.RegisterClassMap<Site>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.SiteId); });
            BsonClassMap.RegisterClassMap<SitePage>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.PageId); });
            BsonClassMap.RegisterClassMap<FriendlyUrl>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.Id); });
            BsonClassMap.RegisterClassMap<ModuleDefinition>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.ModuleDefId); });
            BsonClassMap.RegisterClassMap<Module>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.ModuleId); });
            BsonClassMap.RegisterClassMap<ModuleSetting>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.Id); });
            BsonClassMap.RegisterClassMap<Link>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.ItemId); });
            BsonClassMap.RegisterClassMap<Role>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.RoleId); });
            BsonClassMap.RegisterClassMap<SiteUser>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.UserId); });
            BsonClassMap.RegisterClassMap<UserRole>(cm => { cm.AutoMap(); cm.MapIdMember(c => c.Id); });
            BsonClassMap.RegisterClassMap<PageModule>(cm => cm.AutoMap());

            _classMapsRegistered = true;
        }
    }

    public IMongoCollection<Site> Sites => Db.GetCollection<Site>("mp_Sites");
    public IMongoCollection<SitePage> Pages => Db.GetCollection<SitePage>("mp_Pages");
    public IMongoCollection<FriendlyUrl> FriendlyUrls => Db.GetCollection<FriendlyUrl>("mp_FriendlyUrls");
    public IMongoCollection<ModuleDefinition> ModuleDefinitions => Db.GetCollection<ModuleDefinition>("mp_ModuleDefinitions");
    public IMongoCollection<Module> Modules => Db.GetCollection<Module>("mp_Modules");
    public IMongoCollection<PageModule> PageModules => Db.GetCollection<PageModule>("mp_PageModules");
    public IMongoCollection<ModuleSetting> ModuleSettings => Db.GetCollection<ModuleSetting>("mp_ModuleSettings");
    public IMongoCollection<Link> Links => Db.GetCollection<Link>("mp_Links");
    public IMongoCollection<Role> Roles => Db.GetCollection<Role>("mp_Roles");
    public IMongoCollection<SiteUser> Users => Db.GetCollection<SiteUser>("mp_Users");
    public IMongoCollection<UserRole> UserRoles => Db.GetCollection<UserRole>("mp_UserRoles");
    public IMongoCollection<BsonDocument> Counters => Db.GetCollection<BsonDocument>("counters");

    /// <summary>
    /// Creates the indexes equivalent to the source's PK/FK access patterns:
    /// links by module with (viewOrder,title) sort support, pages by url,
    /// friendly urls by name, module settings by module, users/roles lookups.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        await Links.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Link>(Builders<Link>.IndexKeys.Ascending(l => l.ModuleId)),
                new CreateIndexModel<Link>(
                    Builders<Link>.IndexKeys.Ascending(l => l.ModuleId).Ascending(l => l.ViewOrder).Ascending(l => l.Title),
                    new CreateIndexOptions { Collation = new Collation("en", strength: CollationStrength.Secondary) }),
                new CreateIndexModel<Link>(Builders<Link>.IndexKeys.Ascending(l => l.ItemGuid)),
            ], cancellationToken: ct);

        await Pages.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<SitePage>(Builders<SitePage>.IndexKeys.Ascending(p => p.SiteId).Ascending(p => p.Url)),
                new CreateIndexModel<SitePage>(Builders<SitePage>.IndexKeys.Ascending(p => p.SiteId).Ascending(p => p.PageOrder)),
            ], cancellationToken: ct);

        await FriendlyUrls.Indexes.CreateOneAsync(
            new CreateIndexModel<FriendlyUrl>(Builders<FriendlyUrl>.IndexKeys.Ascending(f => f.SiteId).Ascending(f => f.FriendlyUrlName)), cancellationToken: ct);

        await PageModules.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PageModule>(Builders<PageModule>.IndexKeys.Ascending(p => p.PageId)),
                new CreateIndexModel<PageModule>(Builders<PageModule>.IndexKeys.Ascending(p => p.ModuleId)),
            ], cancellationToken: ct);

        await Modules.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Module>(Builders<Module>.IndexKeys.Ascending(m => m.SiteId)),
                new CreateIndexModel<Module>(Builders<Module>.IndexKeys.Ascending(m => m.ModuleGuid)),
            ], cancellationToken: ct);

        await ModuleSettings.Indexes.CreateOneAsync(
            new CreateIndexModel<ModuleSetting>(
                Builders<ModuleSetting>.IndexKeys.Ascending(s => s.ModuleId).Ascending(s => s.SettingName),
                new CreateIndexOptions { Unique = true }), cancellationToken: ct);

        await Users.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<SiteUser>(Builders<SiteUser>.IndexKeys.Ascending(u => u.SiteId).Ascending(u => u.LoginName)),
                new CreateIndexModel<SiteUser>(Builders<SiteUser>.IndexKeys.Ascending(u => u.SiteId).Ascending(u => u.LoweredEmail)),
                new CreateIndexModel<SiteUser>(Builders<SiteUser>.IndexKeys.Ascending(u => u.UserGuid)),
            ], cancellationToken: ct);

        await UserRoles.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<UserRole>(Builders<UserRole>.IndexKeys.Ascending(u => u.UserId)),
                new CreateIndexModel<UserRole>(Builders<UserRole>.IndexKeys.Ascending(u => u.RoleId)),
            ], cancellationToken: ct);

        await Roles.Indexes.CreateOneAsync(
            new CreateIndexModel<Role>(Builders<Role>.IndexKeys.Ascending(r => r.SiteId).Ascending(r => r.RoleName)), cancellationToken: ct);
    }
}
