
namespace NeoParking.Access.Infrastructure;
using NeoParking.Access.Domain;
using MongoDB.Driver;
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(string connectionString)
        {
            var client = new MongoClient(connectionString);
            var databaseName = MongoUrl.Create(connectionString).DatabaseName ?? "neoparking_access";
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<ClientDocument> Clients => 
            _database.GetCollection<ClientDocument>("clients");
    }
