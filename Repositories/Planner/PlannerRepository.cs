using BambooBrain_Service.Models;
using Microsoft.Azure.Cosmos;

namespace BambooBrain_Service.Repositories.Planner
{
    public class PlannerRepository : IPlannerRepository
    {
        private readonly Container _container;

        public PlannerRepository(CosmosClient cosmosClient, IConfiguration config)
        {
            _container = cosmosClient.GetContainer(
                config["Cosmos:DatabaseName"],
                config["Cosmos:StudyPlansContainer"]
            );
        }

        public async Task<StudyPlan> CreateAsync(StudyPlan plan)
        {
            var response = await _container.CreateItemAsync(
                plan, new PartitionKey(plan.UserId));
            return response.Resource;
        }

        public async Task<StudyPlan?> GetActiveByUserIdAsync(string userId)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.userId = @userId " +
                "AND c.status = 'active' ORDER BY c.createdAt DESC OFFSET 0 LIMIT 1"
            ).WithParameter("@userId", userId);

            var iterator = _container.GetItemQueryIterator<StudyPlan>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(userId)
                });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var plan = response.FirstOrDefault();
                if (plan != null) return plan;
            }
            return null;
        }

        public async Task<StudyPlan?> GetByIdAsync(string id, string userId)
        {
            try
            {
                var response = await _container.ReadItemAsync<StudyPlan>(
                    id, new PartitionKey(userId));
                return response.Resource;
            }
            catch (CosmosException ex)
                when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<StudyPlan> UpdateAsync(StudyPlan plan)
        {
            plan.UpdatedAt = DateTime.UtcNow;
            var response = await _container.ReplaceItemAsync(
                plan, plan.Id, new PartitionKey(plan.UserId));
            return response.Resource;
        }

        public async Task<List<StudyPlan>> GetAllByUserIdAsync(string userId)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.userId = @userId ORDER BY c.createdAt DESC"
            ).WithParameter("@userId", userId);

            var results = new List<StudyPlan>();
            var iterator = _container.GetItemQueryIterator<StudyPlan>(
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
    }
}
