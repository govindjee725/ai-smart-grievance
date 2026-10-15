using AuthService.Models;
using MongoDB.Driver;

namespace AuthService.Data;

public class UserRepository
{
    private readonly IMongoCollection<User> _users;

    public UserRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("users");
    }

    public async Task EnsureIndexesAsync()
    {
        var index = new CreateIndexModel<User>(
            Builders<User>.IndexKeys
                .Ascending(x => x.Email),
            new CreateIndexOptions
            {
                Unique = true,
                Name = "unique_email"
            });

        await _users.Indexes.CreateOneAsync(index);
    }

    public Task<User?> FindByEmailAsync(string email)
    {
        return _users.Find(x => x.Email == email)
            .FirstOrDefaultAsync();
    }

    public Task<User?> FindByIdAsync(string id)
    {
        return _users.Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    public Task CreateAsync(User user)
    {
        return _users.InsertOneAsync(user);
    }
}