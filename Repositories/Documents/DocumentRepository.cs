using Microsoft.Azure.Cosmos;
using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Documents
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly Container _container;

        public DocumentRepository(CosmosClient cosmosClient, IConfiguration config)
        {
            _container = cosmosClient.GetContainer(
                config["Cosmos:DatabaseName"],
                config["Cosmos:DocumentsContainer"]
            );
        }

        public async Task<Document> CreateAsync(Document document)
        {
            var response = await _container.CreateItemAsync(
                document, new PartitionKey(document.UserId));
            return response.Resource;
        }

        public async Task<Document?> GetByIdAsync(string id, string userId)
        {
            try
            {
                var response = await _container.ReadItemAsync<Document>(
                    id, new PartitionKey(userId));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<List<Document>> GetByUserIdAsync(string userId)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.uploadedAt DESC"
            ).WithParameter("@userId", userId);

            var results = new List<Document>();
            var iterator = _container.GetItemQueryIterator<Document>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        public async Task<Document> UpdateAsync(Document document)
        {
            document.UpdatedAt = DateTime.UtcNow;
            var response = await _container.ReplaceItemAsync(
                document, document.Id, new PartitionKey(document.UserId));
            return response.Resource;
        }

        public async Task DeleteAsync(string id, string userId)
        {
            await _container.DeleteItemAsync<Document>(id, new PartitionKey(userId));
        }
    }
}
