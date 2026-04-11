using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Notifications
{
    public interface INotificationRepository
    {
        Task<Notification> CreateAsync(Notification notification);
        Task<List<Notification>> GetByUserIdAsync(
            string userId, int limit = 20);
        Task<int> GetUnreadCountAsync(string userId);
        Task MarkAsReadAsync(string id, string userId);
        Task MarkAllAsReadAsync(string userId);
        Task DeleteAsync(string id, string userId);
    }
}
