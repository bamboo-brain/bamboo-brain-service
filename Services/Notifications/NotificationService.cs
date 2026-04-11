using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Notifications;
using Microsoft.Azure.SignalR.Management;
using Microsoft.AspNetCore.SignalR;

namespace BambooBrain_Service.Services.Notifications
{
    public class NotificationService : INotificationService, IAsyncDisposable
    {
        private readonly INotificationRepository _repo;
        private readonly IConfiguration _config;
        private readonly ILogger<NotificationService> _logger;
        private ServiceHubContext? _hubContext;

        // HSK-specific tips pool
        private static readonly Dictionary<int, List<string>> _tips = new()
        {
            [1] = new() {
            "Practice the 4 tones daily — tone mistakes are the #1 issue for beginners.",
            "Learn 你好, 谢谢, 再见 perfectly before moving on.",
            "Try labeling objects in your home with their Chinese characters."
        },
            [2] = new() {
            "Focus on measure words (量词) — they're essential for everyday speech.",
            "Practice telling time in Chinese today.",
            "Learn the 10 most common verbs: 是, 有, 说, 去, 来, 做, 看, 想, 知道, 觉得"
        },
            [3] = new() {
            "Start reading simple Chinese news headlines today.",
            "Practice using 因为...所以 (because...therefore) in sentences.",
            "Try narrative structures today to improve your flow."
        },
            [4] = new() {
            "Try practicing narrative structures today to improve your flow.",
            "Learn chengyu (成语) — they'll make your Chinese sound natural.",
            "Practice writing emails in Chinese to solidify formal grammar."
        },
            [5] = new() {
            "Read a Chinese news article without a dictionary — note unknown words.",
            "Practice debating topics in Chinese with the speaking AI.",
            "Focus on classical Chinese phrases that appear in HSK 5 texts."
        },
            [6] = new() {
            "Read native Chinese content — novels, WeChat articles, news.",
            "Focus on regional dialect awareness for natural comprehension.",
            "Practice impromptu speaking on unfamiliar topics."
        },
        };

        public NotificationService(
            INotificationRepository repo,
            IConfiguration config,
            ILogger<NotificationService> logger)
        {
            _repo = repo;
            _config = config;
            _logger = logger;
        }

        private async Task<ServiceHubContext> GetHubContextAsync()
        {
            if (_hubContext != null) return _hubContext;

            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(opt =>
                {
                    opt.ConnectionString = _config["SignalR:ConnectionString"]!;
                    opt.ServiceTransportType = ServiceTransportType.Transient;
                })
                .BuildServiceManager();

            _hubContext = await serviceManager.CreateHubContextAsync(
                "NotificationHub", default);
            return _hubContext;
        }

        // ── Core send method ───────────────────────────────────────────────────

        public async Task SendAsync(
            string userId, string type, string title, string message,
            string? resourceId = null, string? resourceType = null,
            string? actionUrl = null)
        {
            // Persist to Cosmos DB
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                ResourceId = resourceId,
                ResourceType = resourceType,
                ActionUrl = actionUrl
            };

            var saved = await _repo.CreateAsync(notification);

            // Push to frontend via SignalR
            try
            {
                var hubContext = await GetHubContextAsync();
                await hubContext.Clients.User(userId).SendAsync(
                    "ReceiveNotification",
                    new
                    {
                        saved.Id,
                        saved.Type,
                        saved.Title,
                        saved.Message,
                        saved.ResourceId,
                        saved.ResourceType,
                        saved.ActionUrl,
                        saved.IsRead,
                        saved.CreatedAt
                    }
                );

                _logger.LogInformation(
                    "Notification sent to user {UserId}: {Title}", userId, title);
            }
            catch (Exception ex)
            {
                // SignalR failure is non-fatal — notification is already in DB
                _logger.LogWarning(ex,
                    "SignalR push failed for user {UserId} — notification saved to DB",
                    userId);
            }
        }

        // ── Convenience methods ────────────────────────────────────────────────

        public async Task SendAchievementAsync(
            string userId, string achievement, string message)
        {
            await SendAsync(userId, "achievement",
                $"New Achievement! 🏆",
                message,
                actionUrl: "/dashboard");
        }

        public async Task SendProcessingCompleteAsync(
            string userId, string documentId, string fileName)
        {
            await SendAsync(userId, "processing_complete",
                "Processing Complete",
                $"{fileName} is now ready for review.",
                resourceId: documentId,
                resourceType: "document",
                actionUrl: $"/library/{documentId}");
        }

        public async Task SendStreakReminderAsync(string userId, int currentStreak)
        {
            var message = currentStreak > 0
                ? $"You're on a {currentStreak}-day streak! Study today to keep it going. 🔥"
                : "You haven't studied today — start a session to build your streak!";

            await SendAsync(userId, "streak_reminder",
                currentStreak > 0 ? "Keep Your Streak!" : "Start Your Streak!",
                message,
                actionUrl: "/study-center");
        }

        public async Task SendPlanAdaptationAsync(
            string userId, string planId, string agentMessage)
        {
            await SendAsync(userId, "adaptation",
                "Master Ling Updated Your Plan 🤖",
                agentMessage,
                resourceId: planId,
                resourceType: "plan",
                actionUrl: "/planner");
        }

        public async Task SendTipAsync(string userId, int hskLevel)
        {
            var levelTips = _tips.GetValueOrDefault(hskLevel, _tips[3]);
            var tip = levelTips[new Random().Next(levelTips.Count)];

            await SendAsync(userId, "tip",
                $"HSK {hskLevel} Tip",
                tip,
                actionUrl: "/study-center");
        }

        // ── CRUD ───────────────────────────────────────────────────────────────

        public async Task<List<Notification>> GetNotificationsAsync(string userId)
            => await _repo.GetByUserIdAsync(userId);

        public async Task<int> GetUnreadCountAsync(string userId)
            => await _repo.GetUnreadCountAsync(userId);

        public async Task MarkAsReadAsync(string id, string userId)
            => await _repo.MarkAsReadAsync(id, userId);

        public async Task MarkAllAsReadAsync(string userId)
            => await _repo.MarkAllAsReadAsync(userId);

        public async Task DeleteAsync(string id, string userId)
            => await _repo.DeleteAsync(id, userId);

        public async ValueTask DisposeAsync()
        {
            if (_hubContext != null)
                await _hubContext.DisposeAsync();
        }
    }
}
