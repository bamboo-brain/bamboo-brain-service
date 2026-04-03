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

        public async Task<(List<Document> items, string? continuationToken, int totalCount)> GetByUserIdAsync(
            string userId,
            int pageSize,
            string? continuationToken,
            string? fileTypeFilter,
            string? searchQuery)
        {
            // Build dynamic query based on filters
            var queryText = "SELECT * FROM c WHERE c.userId = @userId";

            if (!string.IsNullOrEmpty(fileTypeFilter) && fileTypeFilter != "all")
                queryText += " AND c.fileType = @fileType";

            if (!string.IsNullOrEmpty(searchQuery))
                queryText += " AND (CONTAINS(LOWER(c.fileName), @search) OR ARRAY_CONTAINS(c.tags, @search))";

            queryText += " ORDER BY c.uploadedAt DESC";

            var query = new QueryDefinition(queryText)
                .WithParameter("@userId", userId);

            if (!string.IsNullOrEmpty(fileTypeFilter) && fileTypeFilter != "all")
                query = query.WithParameter("@fileType", fileTypeFilter);

            if (!string.IsNullOrEmpty(searchQuery))
                query = query.WithParameter("@search", searchQuery.ToLower());

            var requestOptions = new QueryRequestOptions
            {
                MaxItemCount = pageSize,
                PartitionKey = new PartitionKey(userId)
            };

            var results = new List<Document>();
            string? nextToken = null;

            // Apply continuation token for next page
            var iterator = string.IsNullOrEmpty(continuationToken)
                ? _container.GetItemQueryIterator<Document>(query, requestOptions: requestOptions)
                : _container.GetItemQueryIterator<Document>(query, continuationToken, requestOptions);

            // Only read one page at a time
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
                nextToken = response.ContinuationToken; // null if no more pages
            }

            // Get total count separately for UI pagination display
            var totalCount = await GetTotalCountAsync(userId, fileTypeFilter, searchQuery);

            return (results, nextToken, totalCount);
        }

        public async Task<int> GetTotalCountAsync(
            string userId,
            string? fileTypeFilter,
            string? searchQuery)
        {
            var queryText = "SELECT VALUE COUNT(1) FROM c WHERE c.userId = @userId";

            if (!string.IsNullOrEmpty(fileTypeFilter) && fileTypeFilter != "all")
                queryText += " AND c.fileType = @fileType";

            if (!string.IsNullOrEmpty(searchQuery))
                queryText += " AND (CONTAINS(LOWER(c.fileName), @search) OR ARRAY_CONTAINS(c.tags, @search))";

            var query = new QueryDefinition(queryText)
                .WithParameter("@userId", userId);

            if (!string.IsNullOrEmpty(fileTypeFilter) && fileTypeFilter != "all")
                query = query.WithParameter("@fileType", fileTypeFilter);

            if (!string.IsNullOrEmpty(searchQuery))
                query = query.WithParameter("@search", searchQuery.ToLower());

            var iterator = _container.GetItemQueryIterator<int>(query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(userId)
                });

            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
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
