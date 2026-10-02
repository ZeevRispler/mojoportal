using mojoPortal.Data.Mongo;
using Testcontainers.MongoDb;
using Xunit;

namespace mojoPortal.Tests.Integration;

/// <summary>
/// Real MongoDB via Testcontainers (mongo:7.0). Each test class gets a fresh
/// database on the shared container.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder().WithImage("mongo:7.0").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public MongoStore NewStore(string database)
        => new(new MongoOptions { ConnectionString = ConnectionString, Database = database });
}

[CollectionDefinition("mongo")]
public sealed class MongoCollection : ICollectionFixture<MongoFixture> { }
