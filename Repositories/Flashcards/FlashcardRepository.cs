using BambooBrain_Service.Models;
using Microsoft.Azure.Cosmos;

namespace BambooBrain_Service.Repositories.Flashcards
{
    public class FlashcardRepository : IFlashcardRepository
    {
        private readonly Container _container;

        public FlashcardRepository(CosmosClient cosmosClient, IConfiguration config)
        {
            _container = cosmosClient.GetContainer(
                config["Cosmos:DatabaseName"],
                config["Cosmos:FlashcardDecksContainer"]
            );
        }

        public async Task<FlashcardDeck> CreateAsync(FlashcardDeck deck)
        {
            var response = await _container.CreateItemAsync(
                deck, new PartitionKey(deck.UserId));
            return response.Resource;
        }

        public async Task<FlashcardDeck?> GetByIdAsync(string id, string userId)
        {
            try
            {
                var response = await _container.ReadItemAsync<FlashcardDeck>(
                    id, new PartitionKey(userId));
                return response.Resource;
            }
            catch (CosmosException ex)
                when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<List<FlashcardDeck>> GetByUserIdAsync(string userId)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.updatedAt DESC"
            ).WithParameter("@userId", userId);

            var results = new List<FlashcardDeck>();
            var iterator = _container.GetItemQueryIterator<FlashcardDeck>(
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

        public async Task<FlashcardDeck> UpdateAsync(FlashcardDeck deck)
        {
            deck.UpdatedAt = DateTime.UtcNow;
            var response = await _container.ReplaceItemAsync(
                deck, deck.Id, new PartitionKey(deck.UserId));
            return response.Resource;
        }

        public async Task DeleteAsync(string id, string userId)
        {
            await _container.DeleteItemAsync<FlashcardDeck>(
                id, new PartitionKey(userId));
        }
    }
}
