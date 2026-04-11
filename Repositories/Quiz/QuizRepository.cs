using BambooBrain_Service.Models;
using Microsoft.Azure.Cosmos;

namespace BambooBrain_Service.Repositories.Quiz
{
    public class QuizRepository : IQuizRepository
    {
        private readonly Container _container;

        public QuizRepository(CosmosClient cosmosClient, IConfiguration config)
        {
            _container = cosmosClient.GetContainer(
                config["Cosmos:DatabaseName"],
                config["Cosmos:QuizSessionsContainer"]
            );
        }

        public async Task<QuizSession> CreateAsync(QuizSession session)
        {
            var response = await _container.CreateItemAsync(
                session, new PartitionKey(session.UserId));
            return response.Resource;
        }

        public async Task<QuizSession?> GetByIdAsync(string id, string userId)
        {
            try
            {
                var response = await _container.ReadItemAsync<QuizSession>(
                    id, new PartitionKey(userId));
                return response.Resource;
            }
            catch (CosmosException ex)
                when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<List<QuizSession>> GetByUserIdAsync(
            string userId, int limit = 20)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.userId = @userId " +
                "ORDER BY c.createdAt DESC OFFSET 0 LIMIT @limit"
            )
            .WithParameter("@userId", userId)
            .WithParameter("@limit", limit);

            var results = new List<QuizSession>();
            var iterator = _container.GetItemQueryIterator<QuizSession>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(userId)
                });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        public async Task<QuizSession> UpdateAsync(QuizSession session)
        {
            var response = await _container.ReplaceItemAsync(
                session, session.Id, new PartitionKey(session.UserId));
            return response.Resource;
        }

        public async Task DeleteAsync(string id, string userId)
        {
            await _container.DeleteItemAsync<QuizSession>(
                id, new PartitionKey(userId));
        }
    }
}
