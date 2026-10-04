using MongoDB.Bson;
using MongoDB.Driver;
using mojoPortal.Business;

namespace mojoPortal.Data.Mongo;

/// <summary>
/// Atomic integer sequence generator standing in for T-SQL IDENTITY columns.
/// Each counter document {_id: name, seq: n} is incremented with an atomic
/// findOneAndUpdate, so concurrent inserts never collide.
/// </summary>
public sealed class CounterService : ICounterRepository
{
    private readonly MongoStore _store;

    public CounterService(MongoStore store) => _store = store;

    public async Task<int> NextAsync(string name, int seed, CancellationToken ct = default)
    {
        // seed is the first value to issue (e.g. IDENTITY(0,1) -> 0).
        var doc = await _store.Counters.FindOneAndUpdateAsync<BsonDocument>(
            Builders<BsonDocument>.Filter.Eq("_id", name),
            new BsonDocument
            {
                { "$inc", new BsonDocument("seq", 1) },
                { "$setOnInsert", new BsonDocument("base", seed) },
            },
            new FindOneAndUpdateOptions<BsonDocument>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After,
            },
            ct);

        // First insert lands at seed+1 via $inc on missing seq; rebase so the
        // very first issued id equals seed.
        int seq = doc["seq"].ToInt32();
        int issued = seed + seq - 1;
        return issued;
    }
}
