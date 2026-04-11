using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Notifications
{
    public interface INotificationService
    {
        // Send + persist + push via SignalR
        Task SendAsync(string userId, string type, string title,
            string message, string? resourceId = null,
            string? resourceType = null, string? actionUrl = null);

        // Convenience methods for specific notification types
        Task SendAchievementAsync(string userId, string achievement, string message);
        Task SendProcessingCompleteAsync(string userId, string documentId,
            string fileName);
        Task SendStreakReminderAsync(string userId, int currentStreak);
        Task SendPlanAdaptationAsync(string userId, string planId,
            string agentMessage);
        Task SendTipAsync(string userId, int hskLevel);

        // CRUD
        Task<List<Notification>> GetNotificationsAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task MarkAsReadAsync(string id, string userId);
        Task MarkAllAsReadAsync(string userId);
        Task DeleteAsync(string id, string userId);
    }
}
