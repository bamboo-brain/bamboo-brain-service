using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Planner;
using BambooBrain_Service.Repositories.Stats;

namespace BambooBrain_Service.Services.Planner
{
    public class PlannerService : IPlannerService
    {
        private readonly IPlannerRepository _plans;
        private readonly IStatsRepository _stats;
        private readonly GoalAgent _goalAgent;
        private readonly MonitorAgent _monitorAgent;
        private readonly AdaptAgent _adaptAgent;
        private readonly ILogger<PlannerService> _logger;

        public PlannerService(
            IPlannerRepository plans,
            IStatsRepository stats,
            GoalAgent goalAgent,
            MonitorAgent monitorAgent,
            AdaptAgent adaptAgent,
            ILogger<PlannerService> logger)
        {
            _plans = plans;
            _stats = stats;
            _goalAgent = goalAgent;
            _monitorAgent = monitorAgent;
            _adaptAgent = adaptAgent;
            _logger = logger;
        }

        // ── Agent 1: Create initial plan ───────────────────────────────────────

        public async Task<StudyPlan> CreatePlanAsync(
            string userId, CreatePlanRequest request)
        {
            var userStats = await _stats.GetOrCreateAsync(userId);

            // Run Goal Agent to generate schedule
            _logger.LogInformation("GoalAgent running for user {UserId}", userId);
            var events = await _goalAgent.GeneratePlanAsync(
                request.Goal, request.WeeklySchedule, userStats);

            var plan = new StudyPlan
            {
                UserId = userId,
                Goal = request.Goal,
                WeeklySchedule = request.WeeklySchedule,
                Events = events,
                Status = "active",
                NextAdaptationDue = DateTime.UtcNow.AddDays(7),
                AgentNotes = new List<AgentNote>
            {
                new AgentNote
                {
                    Type = "generation",
                    Message = $"Initial plan generated: {events.Count} events " +
                              $"over 4 weeks targeting HSK {request.Goal.TargetHskLevel}"
                }
            }
            };

            // Sync to calendars if requested
            if (request.SyncToGoogleCalendar && !string.IsNullOrEmpty(request.GoogleAccessToken))
                await SyncToGoogleCalendarAsync(plan, request.GoogleAccessToken);

            if (request.SyncToMicrosoftCalendar &&
                !string.IsNullOrEmpty(request.MicrosoftAccessToken))
                await SyncToMicrosoftCalendarAsync(plan, request.MicrosoftAccessToken);

            return await _plans.CreateAsync(plan);
        }

        // ── Agent 2+3: Weekly adaptation ──────────────────────────────────────

        public async Task<StudyPlan> RunWeeklyAdaptationAsync(
            string planId, string userId)
        {
            var plan = await _plans.GetByIdAsync(planId, userId)
                ?? throw new InvalidOperationException("Plan not found.");

            var userStats = await _stats.GetOrCreateAsync(userId);

            // Agent 2: Monitor — analyze the week
            _logger.LogInformation("MonitorAgent running for plan {PlanId}", planId);
            var analysis = await _monitorAgent.AnalyzeWeekAsync(plan, userStats);

            plan.AgentNotes.Add(new AgentNote
            {
                Type = "observation",
                Message = analysis.StudentMessage
            });

            if (analysis.NeedsAdaptation)
            {
                // Agent 3: Adapt — regenerate next 2 weeks
                _logger.LogInformation("AdaptAgent running for plan {PlanId}", planId);
                var newEvents = await _adaptAgent.RegenerateNextTwoWeeksAsync(
                    plan, analysis, userStats);

                // Remove future scheduled events (keep completed/skipped history)
                plan.Events.RemoveAll(e =>
                    e.Status == "scheduled" &&
                    e.Date > DateTime.UtcNow);

                // Add new events
                plan.Events.AddRange(newEvents);

                plan.AgentNotes.Add(new AgentNote
                {
                    Type = "adaptation",
                    Message = $"Plan adapted: {analysis.AdaptationReason}. " +
                              $"Added {newEvents.Count} new events."
                });

                // Update goal if recommended
                if (analysis.RecommendedDailyMinutes > 0)
                    plan.Goal.DailyStudyMinutes = analysis.RecommendedDailyMinutes;

                if (analysis.RecommendedSessionsPerWeek > 0)
                    plan.WeeklySchedule.SessionsPerWeek =
                        analysis.RecommendedSessionsPerWeek;
            }

            plan.LastAdaptedAt = DateTime.UtcNow;
            plan.NextAdaptationDue = DateTime.UtcNow.AddDays(7);

            return await _plans.UpdateAsync(plan);
        }

        // ── Event management ───────────────────────────────────────────────────

        public async Task<StudyPlan?> GetActivePlanAsync(string userId)
            => await _plans.GetActiveByUserIdAsync(userId);

        public async Task<StudyPlan> UpdateEventStatusAsync(
            string planId, string eventId, string userId, UpdateEventRequest request)
        {
            var plan = await _plans.GetByIdAsync(planId, userId)
                ?? throw new InvalidOperationException("Plan not found.");

            var evt = plan.Events.FirstOrDefault(e => e.Id == eventId)
                ?? throw new InvalidOperationException("Event not found.");

            evt.Status = request.Status;
            if (request.Status == "completed")
                evt.CompletedAt = request.CompletedAt ?? DateTime.UtcNow;
            if (request.LinkedResourceId != null)
            {
                evt.LinkedResourceId = request.LinkedResourceId;
                evt.LinkedResourceType = request.LinkedResourceType;
            }

            // Auto-trigger adaptation check if many events skipped
            var recentSkipped = plan.Events
                .Where(e => e.Date >= DateTime.UtcNow.AddDays(-7) &&
                            e.Status == "skipped")
                .Count();

            if (recentSkipped >= 3 && plan.NextAdaptationDue > DateTime.UtcNow)
            {
                // Bring forward the adaptation
                plan.NextAdaptationDue = DateTime.UtcNow;
                plan.AgentNotes.Add(new AgentNote
                {
                    Type = "observation",
                    Message = "Multiple skipped sessions detected — " +
                              "adaptation scheduled early."
                });
            }

            return await _plans.UpdateAsync(plan);
        }

        public async Task<StudyPlan> RescheduleEventAsync(
            string planId, string eventId, string userId,
            RescheduleEventRequest request)
        {
            var plan = await _plans.GetByIdAsync(planId, userId)
                ?? throw new InvalidOperationException("Plan not found.");

            var evt = plan.Events.FirstOrDefault(e => e.Id == eventId)
                ?? throw new InvalidOperationException("Event not found.");

            evt.Date = request.NewDate;
            evt.StartTime = request.NewStartTime;
            evt.Status = "rescheduled";

            return await _plans.UpdateAsync(plan);
        }

        // ── Stats & activity tracking ──────────────────────────────────────────

        public async Task RecordActivityAsync(
            string userId, RecordActivityRequest request)
        {
            var stats = await _stats.GetOrCreateAsync(userId);
            var today = DateTime.UtcNow.Date;

            // Update streak
            if (stats.LastActivityDate?.Date == today.AddDays(-1))
            {
                stats.CurrentStreak++;
            }
            else if (stats.LastActivityDate?.Date != today)
            {
                stats.CurrentStreak = 1; // reset streak
            }

            if (stats.CurrentStreak > stats.LongestStreak)
                stats.LongestStreak = stats.CurrentStreak;

            stats.LastActivityDate = DateTime.UtcNow;

            // Update totals
            stats.TotalStudyMinutes += request.MinutesSpent;
            stats.StudyMinutesToday += request.MinutesSpent;
            stats.StudyMinutesThisWeek += request.MinutesSpent;

            switch (request.ActivityType)
            {
                case "flashcard_review":
                    stats.TotalFlashcardsReviewed += request.ItemsCompleted;
                    stats.TotalCharactersLearned += request.ItemsCompleted / 3;
                    stats.CharactersThisWeek += request.ItemsCompleted / 3;
                    break;
                case "quiz":
                    stats.TotalQuizzesCompleted++;
                    break;
                case "speaking":
                    stats.TotalSpeakingSessions++;
                    break;
                case "document_upload":
                    stats.TotalDocumentsUploaded++;
                    break;
            }

            // Update daily activity log
            var todayLog = stats.DailyActivity.FirstOrDefault(d => d.Date.Date == today);
            if (todayLog == null)
            {
                todayLog = new DailyActivity { Date = today };
                stats.DailyActivity.Add(todayLog);
            }

            todayLog.MinutesStudied += request.MinutesSpent;

            switch (request.ActivityType)
            {
                case "flashcard_review":
                    todayLog.FlashcardsReviewed += request.ItemsCompleted;
                    break;
                case "quiz":
                    todayLog.QuizzesCompleted++;
                    break;
                case "speaking":
                    todayLog.SpeakingMinutes += request.MinutesSpent;
                    break;
            }

            // Keep only last 90 days of activity
            stats.DailyActivity = stats.DailyActivity
                .Where(d => d.Date >= DateTime.UtcNow.AddDays(-90))
                .OrderByDescending(d => d.Date)
                .ToList();

            // Update streak history
            var todayStreak = stats.StreakHistory
                .FirstOrDefault(s => s.Date.Date == today);
            if (todayStreak == null)
            {
                stats.StreakHistory.Add(new StreakDay
                {
                    Date = today,
                    Active = true,
                    MinutesStudied = request.MinutesSpent
                });
            }
            else
            {
                todayStreak.MinutesStudied += request.MinutesSpent;
            }

            // Keep only last 365 days
            stats.StreakHistory = stats.StreakHistory
                .Where(s => s.Date >= DateTime.UtcNow.AddDays(-365))
                .OrderByDescending(s => s.Date)
                .ToList();

            await _stats.UpdateAsync(stats);
        }

        public async Task<UserStats> GetStatsAsync(string userId)
            => await _stats.GetOrCreateAsync(userId);

        // ── Calendar view ──────────────────────────────────────────────────────

        public async Task<object> GetCalendarEventsAsync(
            string userId, int year, int month)
        {
            var plan = await _plans.GetActiveByUserIdAsync(userId);

            if (plan == null)
                return new { events = new List<object>(), hasPlan = false };

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var monthEvents = plan.Events
                .Where(e => e.Date >= startDate && e.Date <= endDate)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.Description,
                    e.Type,
                    e.Date,
                    e.StartTime,
                    e.DurationMinutes,
                    e.Status,
                    e.Color,
                    e.LinkedResourceId,
                    e.LinkedResourceType
                })
                .OrderBy(e => e.Date)
                .ThenBy(e => e.StartTime)
                .ToList();

            return new
            {
                events = monthEvents,
                hasPlan = true,
                planId = plan.Id,
                goal = plan.Goal,
                adaptationDue = plan.NextAdaptationDue,
                lastAgentNote = plan.AgentNotes.LastOrDefault()?.Message
            };
        }

        // ── Calendar sync ──────────────────────────────────────────────────────

        private async Task SyncToGoogleCalendarAsync(
            StudyPlan plan, string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", accessToken);

                foreach (var evt in plan.Events)
                {
                    var startDateTime = evt.Date.Date +
                        TimeSpan.Parse(evt.StartTime);
                    var endDateTime = startDateTime
                        .AddMinutes(evt.DurationMinutes);

                    var googleEvent = new
                    {
                        summary = evt.Title,
                        description = evt.Description,
                        start = new
                        {
                            dateTime = startDateTime.ToString("o"),
                            timeZone = "UTC"
                        },
                        end = new
                        {
                            dateTime = endDateTime.ToString("o"),
                            timeZone = "UTC"
                        },
                        colorId = evt.Type switch
                        {
                            "speaking" => "7",
                            "quiz" => "6",
                            "milestone" => "2",
                            _ => "10"
                        }
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(googleEvent);
                    var content = new StringContent(json, System.Text.Encoding.UTF8,
                        "application/json");

                    var response = await httpClient.PostAsync(
                        "https://www.googleapis.com/calendar/v3/calendars/primary/events",
                        content);

                    if (response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        var created = System.Text.Json.JsonSerializer
                            .Deserialize<System.Text.Json.JsonElement>(body);
                        evt.GoogleEventId = created.GetProperty("id").GetString();
                    }
                }

                plan.GoogleCalendarSynced = true;
                _logger.LogInformation("Synced {Count} events to Google Calendar",
                    plan.Events.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google Calendar sync failed");
            }
        }

        private async Task SyncToMicrosoftCalendarAsync(
            StudyPlan plan, string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", accessToken);

                foreach (var evt in plan.Events)
                {
                    var startDateTime = evt.Date.Date +
                        TimeSpan.Parse(evt.StartTime);
                    var endDateTime = startDateTime
                        .AddMinutes(evt.DurationMinutes);

                    var msEvent = new
                    {
                        subject = evt.Title,
                        body = new
                        {
                            contentType = "text",
                            content = evt.Description
                        },
                        start = new
                        {
                            dateTime = startDateTime.ToString("o"),
                            timeZone = "UTC"
                        },
                        end = new
                        {
                            dateTime = endDateTime.ToString("o"),
                            timeZone = "UTC"
                        }
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(msEvent);
                    var content = new StringContent(json, System.Text.Encoding.UTF8,
                        "application/json");

                    var response = await httpClient.PostAsync(
                        "https://graph.microsoft.com/v1.0/me/calendar/events",
                        content);

                    if (response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        var created = System.Text.Json.JsonSerializer
                            .Deserialize<System.Text.Json.JsonElement>(body);
                        evt.MicrosoftEventId = created.GetProperty("id").GetString();
                    }
                }

                plan.MicrosoftCalendarSynced = true;
                _logger.LogInformation("Synced {Count} events to Microsoft Calendar",
                    plan.Events.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Microsoft Calendar sync failed");
            }
        }
    }

}
