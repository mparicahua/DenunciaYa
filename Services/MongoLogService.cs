using DenunciaYA.API.Models;
using MongoDB.Driver;

namespace DenunciaYA.API.Services;

public class MongoLogService
{
    private readonly IMongoCollection<ApiLog> _logs;

    public MongoLogService(IConfiguration config)
    {
        var connStr = config.GetConnectionString("MongoDB")!;
        var settings = MongoClientSettings.FromConnectionString(connStr);
        settings.ServerApi = new ServerApi(ServerApiVersion.V1);
        var client = new MongoClient(settings);
        var db = client.GetDatabase("denunciaya_logs");
        _logs = db.GetCollection<ApiLog>("api_logs");
    }

    public Task InsertAsync(ApiLog log) => _logs.InsertOneAsync(log);

    public async Task<List<ApiLog>> GetRecentAsync(int limit = 20) =>
        await _logs.Find(FilterDefinition<ApiLog>.Empty)
            .Sort(Builders<ApiLog>.Sort.Descending(x => x.Timestamp))
            .Limit(limit)
            .ToListAsync();

    public async Task<long> CountAsync() =>
        await _logs.CountDocumentsAsync(FilterDefinition<ApiLog>.Empty);
}
