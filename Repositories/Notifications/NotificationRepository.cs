using BambooBrain_Service.Models;
using Microsoft.Azure.Cosmos;

namespace BambooBrain_Service.Repositories.Notifications
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly Container _container;

        public NotificationRepository(CosmosClient cosmosClient, IConfiguration config)
        {
            _container = cosmosClient.GetContainer(
                config["Cosmos:DatabaseName"],
                config["Cosmos:NotificationsContainer"]
            );
        }

        public async Task<Notification> CreateAsync(Notification notification)
        {
            var response = await _container.CreateItemAsync(
                notification, new PartitionKey(notification.UserId));
            return response.Resource;
        }

        public async Task<List<Notification>> GetByUserIdAsync(
            string userId, int limit = 20)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.userId = @userId " +
                "ORDER BY c.createdAt DESC OFFSET 0 LIMIT @limit"
            )
            .WithParameter("@userId", userId)
            .WithParameter("@limit", limit);

            var results = new List<Notification>();
            var iterator = _container.GetItemQueryIterator<Notification>(
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

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            var query = new QueryDefinition(
                "SELECT VALUE COUNT(1) FROM c WHERE c.userId = @userId AND c.isRead = false"
            ).WithParameter("@userId", userId);

            var iterator = _container.GetItemQueryIterator<int>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(userId)
                });

            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }

        public async Task MarkAsReadAsync(string id, string userId)
        {
            var notification = await _container.ReadItemAsync<Notification>(
                id, new PartitionKey(userId));
            notification.Resource.IsRead = true;
            await _container.ReplaceItemAsync(
                notification.Resource, id, new PartitionKey(userId));
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var notifications = await GetByUserIdAsync(userId, 100);
            var unread = notifications.Where(n => !n.IsRead).ToList();

            foreach (var n in unread)
            {
                n.IsRead = true;
                await _container.ReplaceItemAsync(n, n.Id, new PartitionKey(userId));
            }
        }

        public async Task DeleteAsync(string id, string userId)
        {
            await _container.DeleteItemAsync<Notification>(
                id, new PartitionKey(userId));
        }
    }
}
