using BambooBrain_Service.Repositories.Planner;
using BambooBrain_Service.Repositories.Stats;
using BambooBrain_Service.Services.Notifications;
using BambooBrain_Service.Services.Planner;
using BambooBrain_Service.Services.Search;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace BambooBrain_Service.Services.Agents
{
    public class BambooBrainTools
    {
        private readonly IPlannerRepository _plans;
        private readonly IStatsRepository _stats;
        private readonly IAISearchService _search;
        private readonly IPlannerService _planner;
        private readonly INotificationService _notifications;
        private readonly ILogger<BambooBrainTools> _logger;

        // UserId injected per-request
        public string UserId { get; set; } = string.Empty;

        public BambooBrainTools(
            IPlannerRepository plans,
            IStatsRepository stats,
            IAISearchService search,
            IPlannerService planner,
            INotificationService notifications,
            ILogger<BambooBrainTools> logger)
        {
            _plans = plans;
            _stats = stats;
            _search = search;
            _planner = planner;
            _notifications = notifications;
            _logger = logger;
        }

        // ── Tool 1: Get user stats ─────────────────────────────────────────────

        [KernelFunction("get_user_stats")]
        [Description("Get the user's study statistics including streak, vocabulary learned, quiz scores, speaking sessions, and HSK progress")]
        public async Task<string> GetUserStatsAsync()
        {
            var stats = await _stats.GetOrCreateAsync(UserId);
            return JsonSerializer.Serialize(new
            {
                currentStreak = stats.CurrentStreak,
                longestStreak = stats.LongestStreak,
                totalCharactersLearned = stats.TotalCharactersLearned,
                charactersThisWeek = stats.CharactersThisWeek,
                totalStudyMinutes = stats.TotalStudyMinutes,
                studyMinutesThisWeek = stats.StudyMinutesThisWeek,
                studyMinutesToday = stats.StudyMinutesToday,
                totalFlashcardsReviewed = stats.TotalFlashcardsReviewed,
                totalQuizzesCompleted = stats.TotalQuizzesCompleted,
                totalSpeakingSessions = stats.TotalSpeakingSessions,
                hskProgress = stats.HskProgress.Select(h => new
                {
                    level = h.Level,
                    wordsLearned = h.WordsLearned,
                    totalWords = h.TotalWords,
                    percentage = h.Percentage
                })
            });
        }

        // ── Tool 2: Get active study plan ──────────────────────────────────────

        [KernelFunction("get_active_plan")]
        [Description("Get the user's current active study plan including goals, upcoming scheduled events, and any overdue or skipped sessions")]
        public async Task<string> GetActivePlanAsync()
        {
            var plan = await _plans.GetActiveByUserIdAsync(UserId);
            if (plan == null)
                return JsonSerializer.Serialize(new { hasPlan = false });

            var upcomingEvents = plan.Events
                .Where(e => e.Date >= DateTime.UtcNow.Date &&
                            e.Status == "scheduled")
                .OrderBy(e => e.Date)
                .Take(7)
                .Select(e => new
                {
                    e.Title,
                    e.Type,
                    e.Date,
                    e.StartTime,
                    e.DurationMinutes,
                    e.Status
                });

            var recentSkipped = plan.Events
                .Where(e => e.Date >= DateTime.UtcNow.AddDays(-7) &&
                            e.Status == "skipped")
                .Count();

            var recentCompleted = plan.Events
                .Where(e => e.Date >= DateTime.UtcNow.AddDays(-7) &&
                            e.Status == "completed")
                .Count();

            return JsonSerializer.Serialize(new
            {
                hasPlan = true,
                planId = plan.Id,
                goal = new
                {
                    currentHskLevel = plan.Goal.CurrentHskLevel,
                    targetHskLevel = plan.Goal.TargetHskLevel,
                    targetDate = plan.Goal.TargetDate,
                    dailyStudyMinutes = plan.Goal.DailyStudyMinutes,
                    focusAreas = plan.Goal.FocusAreas
                },
                weeklySchedule = plan.WeeklySchedule,
                upcomingEvents,
                lastWeekStats = new
                {
                    completed = recentCompleted,
                    skipped = recentSkipped,
                    completionRate = plan.Events
                        .Where(e => e.Date >= DateTime.UtcNow.AddDays(-7))
                        .Any() ? Math.Round((double)recentCompleted /
                            plan.Events.Count(e =>
                                e.Date >= DateTime.UtcNow.AddDays(-7)) * 100, 1) : 0
                },
                adaptationDue = plan.NextAdaptationDue <= DateTime.UtcNow,
                lastAgentNote = plan.AgentNotes.LastOrDefault()?.Message
            });
        }

        // ── Tool 3: Search documents ───────────────────────────────────────────

        [KernelFunction("search_user_documents")]
        [Description("Search the user's uploaded documents for relevant vocabulary, topics, or content. Use this to find what materials the user has studied")]
        public async Task<string> SearchDocumentsAsync(
            [Description("The search query")] string query,
            [Description("Maximum number of results")] int top = 5)
        {
            var result = await _search.SearchDocumentsAsync(UserId, query, top);
            return JsonSerializer.Serialize(new
            {
                query,
                totalFound = result.TotalCount,
                documents = result.Hits.Select(h => new
                {
                    h.DocumentId,
                    h.DocumentTitle,
                    h.DocumentType,
                    h.HskLevel,
                    topVocabulary = h.TopWords.Select(w => new
                    {
                        w.Word,
                        w.Pinyin,
                        w.Meaning,
                        w.HskLevel
                    })
                })
            });
        }

        // ── Tool 4: Adapt study plan ───────────────────────────────────────────

        [KernelFunction("adapt_study_plan")]
        [Description("Trigger the AI adaptation of the user's study plan based on recent performance. Use this when the user needs schedule adjustments")]
        public async Task<string> AdaptStudyPlanAsync(
            [Description("The plan ID to adapt")] string planId)
        {
            try
            {
                var plan = await _planner.RunWeeklyAdaptationAsync(planId, UserId);
                var lastNote = plan.AgentNotes.LastOrDefault(
                    n => n.Type == "adaptation");
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = lastNote?.Message ?? "Plan adapted successfully.",
                    newEventCount = plan.Events.Count(e => e.Status == "scheduled")
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        // ── Tool 5: Send notification ──────────────────────────────────────────

        [KernelFunction("send_notification")]
        [Description("Send a notification to the user. Use this to inform them about important insights or actions taken")]
        public async Task<string> SendNotificationAsync(
            [Description("Notification title")] string title,
            [Description("Notification message")] string message,
            [Description("Notification type: achievement, tip, adaptation, or system")]
        string type = "tip")
        {
            await _notifications.SendAsync(UserId, type, title, message,
                actionUrl: "/dashboard");
            return JsonSerializer.Serialize(new
            {
                sent = true,
                title,
                message
            });
        }

        // ── Tool 6: Get HSK vocabulary gap ─────────────────────────────────────

        [KernelFunction("get_vocabulary_gap")]
        [Description("Calculate how many words the user needs to learn to reach their target HSK level")]
        public async Task<string> GetVocabularyGapAsync()
        {
            var stats = await _stats.GetOrCreateAsync(UserId);
            var plan = await _plans.GetActiveByUserIdAsync(UserId);

            var targetLevel = plan?.Goal.TargetHskLevel ?? 4;
            var currentLevel = plan?.Goal.CurrentHskLevel ?? 1;

            var wordsNeeded = new Dictionary<int, int>
            {
                [1] = 150,
                [2] = 150,
                [3] = 300,
                [4] = 600,
                [5] = 1300,
                [6] = 2500
            };

            var totalWordsForTarget = Enumerable.Range(1, targetLevel)
                .Sum(l => wordsNeeded.GetValueOrDefault(l, 0));
            var gap = totalWordsForTarget - stats.TotalCharactersLearned;

            return JsonSerializer.Serialize(new
            {
                currentLevel,
                targetLevel,
                wordsLearned = stats.TotalCharactersLearned,
                wordsNeededForTarget = totalWordsForTarget,
                gap = Math.Max(0, gap),
                estimatedWeeksAtCurrentPace = stats.CharactersThisWeek > 0
                    ? Math.Round((double)gap / stats.CharactersThisWeek, 1)
                    : -1
            });
        }
    }
}
