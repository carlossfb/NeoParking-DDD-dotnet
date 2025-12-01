using MongoDB.Driver;
using main.infrastructure.persistence.mongo.model;

namespace main.infrastructure.persistence.mongo
{
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
}