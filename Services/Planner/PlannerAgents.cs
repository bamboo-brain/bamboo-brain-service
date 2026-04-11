using Azure;
using Azure.AI.OpenAI;
using BambooBrain_Service.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Text.Json;

namespace BambooBrain_Service.Services.Planner
{
    /// <summary>
    /// Agent 1: Interviews user and generates an initial study plan
    /// </summary>
    public class GoalAgent
    {
        private readonly AzureOpenAIClient _aiClient;
        private readonly ILogger<GoalAgent> _logger;
        private readonly IConfiguration _config;

        public GoalAgent(ILogger<GoalAgent> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;

            _aiClient = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!),
                new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            );
        }

        public async Task<List<StudyEvent>> GeneratePlanAsync(
            StudyGoal goal,
            WeeklySchedule schedule,
            UserStats stats)
        {
            var weeksUntilTarget = goal.TargetDate.HasValue
                ? Math.Max(1, (int)(goal.TargetDate.Value - DateTime.UtcNow).TotalDays / 7)
                : 12;

            var levelGap = goal.TargetHskLevel - goal.CurrentHskLevel;

            var prompt = $$"""
            You are an expert Chinese language study planner.
            Create a detailed study schedule for the next 4 weeks.

            Student profile:
            - Current HSK level: {{goal.CurrentHskLevel}}
            - Target HSK level: {{goal.TargetHskLevel}}
            - Weeks until target: {{weeksUntilTarget}}
            - Daily study time: {{goal.DailyStudyMinutes}} minutes
            - Focus areas: {{string.Join(", ", goal.FocusAreas)}}
            - Reason for learning: {{goal.ReasonForLearning}}
            - Total characters learned so far: {{stats.TotalCharactersLearned}}
            - Current streak: {{stats.CurrentStreak}} days
            - Preferred days: {{string.Join(", ", schedule.PreferredDays)}}
            - Preferred time: {{schedule.PreferredTime}}
            - Sessions per week: {{schedule.SessionsPerWeek}}

            Create study events for the next 4 weeks starting from today ({{DateTime.UtcNow:yyyy-MM-dd}}).

            Rules:
            - Only schedule events on preferred days
            - Mix activity types: flashcards, speaking, quiz, reading, review
            - Include weekly milestone review every Sunday
            - Increase difficulty progressively
            - If focusing on speaking, weight more speaking sessions
            - Keep durations within daily study time limit

            Return ONLY a JSON array of events. No markdown.
            Each event:
            {
              "title": "string",
              "description": "string (specific, actionable)",
              "type": "flashcards|speaking|quiz|reading|review|milestone",
              "date": "YYYY-MM-DD",
              "startTime": "HH:MM",
              "durationMinutes": number,
              "color": "#166534 for flashcards, #1d4ed8 for speaking, #b45309 for quiz, #6d28d9 for reading, #374151 for review, #065f46 for milestone"
            }

            Generate exactly {{schedule.SessionsPerWeek * 4}} study events plus 4 weekly milestones.
            """;

            var chatClient = _aiClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage("You are an expert study planner. Always respond with valid JSON only."),
                    new UserChatMessage(prompt)
                        },
                        new ChatCompletionOptions
                        {
                            MaxOutputTokenCount = 4000,
                            Temperature = 0.4f
                        }
            );

            var content = response.Value.Content[0].Text
                .Replace("```json", "").Replace("```", "").Trim();

            try
            {
                var events = JsonSerializer.Deserialize<List<StudyEvent>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new();

                _logger.LogInformation("GoalAgent generated {Count} events", events.Count);
                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GoalAgent failed to parse events");
                return new();
            }
        }
    }

    /// <summary>
    /// Agent 2: Weekly monitor — analyzes performance and decides if re-planning needed
    /// </summary>
    public class MonitorAgent
    {
        private readonly AzureOpenAIClient _aiClient;
        private readonly ILogger<MonitorAgent> _logger;
        private readonly IConfiguration _config;

        public MonitorAgent(ILogger<MonitorAgent> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;

            _aiClient = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!),
                new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            );
        }

        public async Task<MonitorAnalysis> AnalyzeWeekAsync(
            StudyPlan plan,
            UserStats stats)
        {
            var lastWeekEvents = plan.Events
                .Where(e => e.Date >= DateTime.UtcNow.AddDays(-7) &&
                            e.Date <= DateTime.UtcNow)
                .ToList();

            var completedCount = lastWeekEvents.Count(e => e.Status == "completed");
            var skippedCount = lastWeekEvents.Count(e => e.Status == "skipped");
            var scheduledCount = lastWeekEvents.Count(e => e.Status == "scheduled");
            var completionRate = lastWeekEvents.Any()
                ? (double)completedCount / lastWeekEvents.Count * 100
                : 0;

            var prompt = $$"""
            You are an AI study coach monitoring a student's progress.
            Analyze their week and decide if their study plan needs adjustment.

            Plan goal: HSK {{plan.Goal.CurrentHskLevel}} → HSK {{plan.Goal.TargetHskLevel}}
            Target date: {{(plan.Goal.TargetDate.HasValue ? plan.Goal.TargetDate.Value.ToString("yyyy-MM-dd") : "flexible")}}

            This week's performance:
            - Events completed: {{completedCount}}/{{lastWeekEvents.Count}}
            - Events skipped: {{skippedCount}}
            - Completion rate: {{completionRate:F1}}%
            - Study minutes this week: {{stats.StudyMinutesThisWeek}}
            - Characters learned this week: {{stats.CharactersThisWeek}}
            - Current streak: {{stats.CurrentStreak}} days
            - Flashcards reviewed: {{stats.TotalFlashcardsReviewed}}
            - Quiz average score: based on recent activity
            - Speaking sessions: {{stats.TotalSpeakingSessions}}

            Decide:
            1. Should the plan be adapted? (true/false)
            2. What is the adaptation reason?
            3. What adjustments should be made?
            4. Write an encouraging message to the student

            Return ONLY JSON:
            {
              "needsAdaptation": boolean,
              "adaptationReason": "string",
              "adjustments": ["string array of specific changes"],
              "studentMessage": "encouraging personalized message",
              "recommendedSessionsPerWeek": number,
              "recommendedDailyMinutes": number,
              "focusShift": "string or null (e.g. 'more speaking practice')"
            }
            """;

            var chatClient = _aiClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage("You are an AI study coach. Always respond with valid JSON only."),
                    new UserChatMessage(prompt)
                        },
                        new ChatCompletionOptions
                        {
                            MaxOutputTokenCount = 1000,
                            Temperature = 0.3f
                        }
            );

            var content = response.Value.Content[0].Text
                .Replace("```json", "").Replace("```", "").Trim();

            try
            {
                return JsonSerializer.Deserialize<MonitorAnalysis>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new MonitorAnalysis { NeedsAdaptation = false };
            }
            catch
            {
                return new MonitorAnalysis { NeedsAdaptation = false };
            }
        }
    }

    /// <summary>
    /// Agent 3: Re-generates the next 2 weeks when adaptation is needed
    /// </summary>
    public class AdaptAgent
    {
        private readonly AzureOpenAIClient _aiClient;
        private readonly ILogger<AdaptAgent> _logger;
        private readonly IConfiguration _config;

        public AdaptAgent(ILogger<AdaptAgent> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;

            _aiClient = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!),
                new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            );
        }

        public async Task<List<StudyEvent>> RegenerateNextTwoWeeksAsync(
            StudyPlan plan,
            MonitorAnalysis analysis,
            UserStats stats)
        {
            var prompt = $$"""
            You are an expert Chinese study planner adapting a student's schedule.

            Original goal: HSK {{plan.Goal.CurrentHskLevel}} → HSK {{plan.Goal.TargetHskLevel}}
            Original daily minutes: {{plan.Goal.DailyStudyMinutes}}
            Preferred days: {{string.Join(", ", plan.WeeklySchedule.PreferredDays)}}
            Preferred time: {{plan.WeeklySchedule.PreferredTime}}

            Adaptation reason: {{analysis.AdaptationReason}}
            Requested adjustments: {{string.Join("; ", analysis.Adjustments ?? new())}}
            Focus shift: {{analysis.FocusShift ?? "none"}}
            New recommended sessions/week: {{analysis.RecommendedSessionsPerWeek}}
            New recommended daily minutes: {{analysis.RecommendedDailyMinutes}}

            Student stats:
            - Current streak: {{stats.CurrentStreak}} days
            - Characters learned: {{stats.TotalCharactersLearned}}
            - Study minutes this week: {{stats.StudyMinutesThisWeek}}

            Generate a revised schedule for the NEXT 2 WEEKS starting from tomorrow ({{DateTime.UtcNow.AddDays(1):yyyy-MM-dd}}).
            Be specific and actionable in descriptions.
            Adjust difficulty and activity mix based on the adaptation reason.

            Return ONLY a JSON array of events with same schema as before.
            """;

            var chatClient = _aiClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage("You are an expert study planner. Always respond with valid JSON only."),
                    new UserChatMessage(prompt)
                        },
                        new ChatCompletionOptions
                        {
                            MaxOutputTokenCount = 3000,
                            Temperature = 0.4f
                        }
            );

            var content = response.Value.Content[0].Text
                .Replace("```json", "").Replace("```", "").Trim();

            try
            {
                return JsonSerializer.Deserialize<List<StudyEvent>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new();
            }
            catch
            {
                return new();
            }
        }
    }

    public class MonitorAnalysis
    {
        public bool NeedsAdaptation { get; set; }
        public string AdaptationReason { get; set; } = string.Empty;
        public List<string>? Adjustments { get; set; }
        public string StudentMessage { get; set; } = string.Empty;
        public int RecommendedSessionsPerWeek { get; set; } = 5;
        public int RecommendedDailyMinutes { get; set; } = 30;
        public string? FocusShift { get; set; }
    }

}
