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

        public async Task<Models.User?> GetByIdAsync(string id)
        {
            try
            {
                var response = await _container.ReadItemAsync<Models.User>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<Models.User> UpdateAsync(Models.User user)
        {
            var response = await _container.ReplaceItemAsync(user, user.Id, new PartitionKey(user.Id));
            return response.Resource;
        }

        public async Task<Models.User> UpsertOAuthUserAsync(UpsertOAuthUserRequest request)
        {
            // Check if user already exists
            var existing = await GetByEmailAsync(request.Email);

            if (existing != null)
            {
                // User exists — just return them without modifying anything
                // (preserve their onboarding state and settings)
                return existing;
            }

            // New OAuth user — create them
            var user = new Models.User
            {
                Email = request.Email.ToLower(),
                Name = request.Name,
                Image = request.Image,
                Provider = request.Provider,
                PasswordHash = string.Empty, // no password for OAuth users
                IsOnboardingComplete = false
            };

            return await CreateAsync(user);
        }
    }
}