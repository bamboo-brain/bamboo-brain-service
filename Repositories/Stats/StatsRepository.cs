using BambooBrain_Service.Models;
using Microsoft.Azure.Cosmos;

namespace BambooBrain_Service.Repositories.Stats
{
    public class StatsRepository : IStatsRepository
    {
        private readonly Container _container;

        public StatsRepository(CosmosClient cosmosClient, IConfiguration config)
        {
            _container = cosmosClient.GetContainer(
                config["Cosmos:DatabaseName"],
                config["Cosmos:UserStatsContainer"]
            );
        }

        public async Task<UserStats> GetOrCreateAsync(string userId)
        {
            try
            {
                var response = await _container.ReadItemAsync<UserStats>(
                    userId, new PartitionKey(userId));
                return response.Resource;
            }
            catch (CosmosException ex)
                when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Create default stats for new user
                var stats = new UserStats
                {
                    Id = userId,
                    UserId = userId,
                    HskProgress = Enumerable.Range(1, 6).Select(level => new HskLevelProgress
                    {
                        Level = level,
                        WordsLearned = 0,
                        TotalWords = level switch
                        {
                            1 => 150,
                            2 => 150,
                            3 => 300,
                            4 => 600,
                            5 => 1300,
                            _ => 2500
                        }
                    }).ToList()
                };

                var created = await _container.CreateItemAsync(
                    stats, new PartitionKey(userId));
                return created.Resource;
            }
        }

        public async Task<UserStats> UpdateAsync(UserStats stats)
        {
            stats.UpdatedAt = DateTime.UtcNow;
            var response = await _container.ReplaceItemAsync(
                stats, stats.Id, new PartitionKey(stats.UserId));
            return response.Resource;
        }
    }
}
