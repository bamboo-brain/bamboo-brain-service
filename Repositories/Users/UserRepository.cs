using Microsoft.Azure.Cosmos;
using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Users
{
    public class UserRepository : IUserRepository
    {
        private readonly Container _container;

        public UserRepository(CosmosClient cosmosClient, IConfiguration config)
        {
            var dbName = config["Cosmos:DatabaseName"]!;
            var containerName = config["Cosmos:UsersContainer"]!;
            _container = cosmosClient.GetContainer(dbName, containerName);
        }

        public async Task<Models.User?> GetByEmailAsync(string email)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.email = @email"
            ).WithParameter("@email", email.ToLower());

            var iterator = _container.GetItemQueryIterator<BambooBrain_Service.Models.User>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var user = response.FirstOrDefault();
                if (user != null) return user;
            }
            return null;
        }

        public async Task<Models.User> CreateAsync(Models.User user)
        {
            user.Email = user.Email.ToLower();
            var response = await _container.CreateItemAsync(user, new PartitionKey(user.Id));
            return response.Resource;
        }
    }
}