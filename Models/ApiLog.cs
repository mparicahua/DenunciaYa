using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DenunciaYA.API.Models;

public class ApiLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public DateTime Timestamp { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? Error { get; set; }
}
