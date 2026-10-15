using GrievanceService.Models;
using MongoDB.Driver;

namespace GrievanceService.Data;

public class GrievanceRepository
{
    private readonly IMongoCollection<Grievance> _collection;

    public GrievanceRepository(IMongoDatabase database)
    {
        _collection = database
            .GetCollection<Grievance>("grievances");
    }

    public async Task EnsureIndexesAsync()
    {
        var index = new CreateIndexModel<Grievance>(
            Builders<Grievance>.IndexKeys
                .Ascending(x => x.TrackingId),
            new CreateIndexOptions
            {
                Unique = true,
                Name = "unique_tracking_id"
            });

        await _collection.Indexes.CreateOneAsync(index);

        var citizenIndex = new CreateIndexModel<Grievance>(
            Builders<Grievance>.IndexKeys
                .Ascending(x => x.CitizenId)
                .Descending(x => x.CreatedAt));

        await _collection.Indexes.CreateOneAsync(citizenIndex);
    }

    public Task CreateAsync(Grievance grievance) =>
        _collection.InsertOneAsync(grievance);

    public Task<List<Grievance>> GetByCitizenAsync(string citizenId) =>
        _collection.Find(x => x.CitizenId == citizenId)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

    public Task<Grievance?> GetByTrackingIdAsync(string trackingId) =>
        _collection.Find(x => x.TrackingId == trackingId)
            .FirstOrDefaultAsync();

    public Task<List<Grievance>> GetAllAsync() =>
        _collection.Find(Builders<Grievance>.Filter.Empty)
            .SortByDescending(x => x.CreatedAt)
            .Limit(500)
            .ToListAsync();

    public Task SaveAsync(Grievance grievance) =>
        _collection.ReplaceOneAsync(
            x => x.Id == grievance.Id,
            grievance);
}