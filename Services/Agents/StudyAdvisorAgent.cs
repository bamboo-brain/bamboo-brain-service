using Azure;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Newtonsoft.Json;
using System;
using System.IO;

namespace BambooBrain_Service.Services.Agents
{
    public class StudyAdvisorAgent : IStudyAdvisorAgent
    {
        private readonly BambooBrainTools _tools;
        private readonly AzureOpenAIClient _aiClient;
        private readonly string _deploymentName;
        private readonly ILogger<StudyAdvisorAgent> _logger;

        public StudyAdvisorAgent(
            BambooBrainTools tools,
            IConfiguration config,
            ILogger<StudyAdvisorAgent> logger)
        {
            _tools = tools;
            _logger = logger;
            _deploymentName = config["AzureOpenAI:DeploymentName"]!;

            // ← Same pattern as AudioExtractionService
            _aiClient = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!),
                new AzureOpenAIClientOptions(
                    AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            );
        }

        public async Task<StudyAdvisorResponse> ChatAsync(string userId, string message, List<AgentMessage>? history = null)
        {
            _tools.UserId = userId;
            var actionsPerformed = new List<string>();
            var planAdapted = false;

            try
            {
                _logger.LogInformation(
                    "[Advisor] Processing: {Message}", message);

                // ── Step 1: Decide which tools to call based on the question ──────

                var lowerMsg = message.ToLower();

                var shouldGetStats =
                    lowerMsg.Contains("doing") || lowerMsg.Contains("progress") ||
                    lowerMsg.Contains("streak") || lowerMsg.Contains("week") ||
                    lowerMsg.Contains("stats") || lowerMsg.Contains("score") ||
                    lowerMsg.Contains("learn") || lowerMsg.Contains("study") ||
                    lowerMsg.Contains("how am") || lowerMsg.Contains("performance");

                var shouldGetPlan =
                    lowerMsg.Contains("plan") || lowerMsg.Contains("schedule") ||
                    lowerMsg.Contains("session") || lowerMsg.Contains("today") ||
                    lowerMsg.Contains("next") || lowerMsg.Contains("upcoming") ||
                    lowerMsg.Contains("adjust") || lowerMsg.Contains("change") ||
                    lowerMsg.Contains("struggling") || lowerMsg.Contains("behind");

                var shouldGetVocabGap =
                    lowerMsg.Contains("hsk") || lowerMsg.Contains("gap") ||
                    lowerMsg.Contains("level") || lowerMsg.Contains("words") ||
                    lowerMsg.Contains("vocabulary") || lowerMsg.Contains("far");

                var shouldSearch =
                    lowerMsg.Contains("document") || lowerMsg.Contains("file") ||
                    lowerMsg.Contains("upload") || lowerMsg.Contains("pdf") ||
                    lowerMsg.Contains("content") || lowerMsg.Contains("material");

                var shouldAdapt =
                    lowerMsg.Contains("adjust") || lowerMsg.Contains("update") ||
                    lowerMsg.Contains("change my plan") || lowerMsg.Contains("fix my") ||
                    lowerMsg.Contains("reschedule") || lowerMsg.Contains("adapt") ||
                    lowerMsg.Contains("too hard") || lowerMsg.Contains("too easy") ||
                    lowerMsg.Contains("struggling") || lowerMsg.Contains("back on track");

                // Default: if question is general, get stats + plan
                if (!shouldGetStats && !shouldGetPlan && !shouldGetVocabGap &&
                    !shouldSearch && !shouldAdapt)
                {
                    shouldGetStats = true;
                    shouldGetPlan = true;
                }

                // ── Step 2: Call relevant tools and collect context ───────────────

                var contextParts = new List<string>();

                if (shouldGetStats)
                {
                    _logger.LogInformation("[Advisor] Calling get_user_stats");
                    var stats = await _tools.GetUserStatsAsync();
                    contextParts.Add($"USER STATS:\n{stats}");
                    actionsPerformed.Add("get_user_stats");
                }

                if (shouldGetPlan)
                {
                    _logger.LogInformation("[Advisor] Calling get_active_plan");
                    var plan = await _tools.GetActivePlanAsync();
                    contextParts.Add($"ACTIVE STUDY PLAN:\n{plan}");
                    actionsPerformed.Add("get_active_plan");
                }

                if (shouldGetVocabGap)
                {
                    _logger.LogInformation("[Advisor] Calling get_vocabulary_gap");
                    var gap = await _tools.GetVocabularyGapAsync();
                    contextParts.Add($"VOCABULARY GAP:\n{gap}");
                    actionsPerformed.Add("get_vocabulary_gap");
                }

                if (shouldSearch)
                {
                    _logger.LogInformation("[Advisor] Calling search_user_documents");
                    var docs = await _tools.SearchDocumentsAsync(message, 3);
                    contextParts.Add($"RELEVANT DOCUMENTS:\n{docs}");
                    actionsPerformed.Add("search_user_documents");
                }

                if (shouldAdapt)
                {
                    // Get plan first to find planId
                    if (!actionsPerformed.Contains("get_active_plan"))
                    {
                        var plan = await _tools.GetActivePlanAsync();
                        contextParts.Add($"ACTIVE STUDY PLAN:\n{plan}");
                        actionsPerformed.Add("get_active_plan");
                    }

                    // Extract planId from the plan context
                    var planContext = contextParts
                        .FirstOrDefault(c => c.Contains("planId"));

                    if (planContext != null)
                    {
                        var planIdMatch = System.Text.RegularExpressions.Regex.Match(
                            planContext, @"""planId""\s*:\s*""([^""]+)""");

                        if (planIdMatch.Success)
                        {
                            _logger.LogInformation("[Advisor] Calling adapt_study_plan");
                            var adaptation = await _tools.AdaptStudyPlanAsync(
                                planIdMatch.Groups[1].Value);
                            contextParts.Add($"PLAN ADAPTATION RESULT:\n{adaptation}");
                            actionsPerformed.Add("adapt_study_plan");
                            planAdapted = true;

                            // Send notification about the adaptation
                            await _tools.SendNotificationAsync(
                                "Master Ling Updated Your Plan 🤖",
                                "I reviewed your progress and adjusted your study schedule.",
                                "adaptation");
                            actionsPerformed.Add("send_notification");
                        }
                    }
                }

                // ── Step 3: Build GPT-4o prompt with all gathered context ─────────

                var combinedContext = string.Join("\n\n---\n\n", contextParts);

                var systemPrompt = $"""
                    You are Master Ling, a warm and encouraging Chinese language study advisor
                    for BambooBrain. You have already gathered real data about the user.

                    REAL DATA FROM USER'S ACCOUNT:
                    {combinedContext}

                    Instructions:
                    - Answer using ONLY the real data above — never make up numbers
                    - Be specific: quote actual streak numbers, words learned, completion rates
                    - Be encouraging and actionable
                    - If plan was adapted, mention what changed
                    - Keep response to 2-3 paragraphs maximum
                    - Respond in English unless the user writes in Chinese
                    """;

                // Build conversation history
                var messages = new List<OpenAI.Chat.ChatMessage>
                {
                    new OpenAI.Chat.SystemChatMessage(systemPrompt)
                };

                if (history != null)
                {
                    foreach (var msg in history.TakeLast(4))
                    {
                        if (msg.Role == "user")
                            messages.Add(new OpenAI.Chat.UserChatMessage(msg.Content));
                        else
                            messages.Add(new OpenAI.Chat.AssistantChatMessage(msg.Content));
                    }
                }

                messages.Add(new OpenAI.Chat.UserChatMessage(message));

                // ── Step 4: Call GPT-4o for final response ─────────────────────────

                _logger.LogInformation(
                    "[Advisor] Calling GPT-4o with {Count} context parts",
                    contextParts.Count);

                var chatClient = _aiClient.GetChatClient(_deploymentName);
                var response = await chatClient.CompleteChatAsync(
                    messages,
                    new OpenAI.Chat.ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 1000,
                        Temperature = 0.5f
                    });

                var answer = response.Value.Content[0].Text;

                _logger.LogInformation(
                    "[Advisor] Done. Tools used: {Tools}",
                    string.Join(", ", actionsPerformed));

                return new StudyAdvisorResponse
                {
                    Answer = answer,
                    ActionsPerformed = actionsPerformed,
                    PlanWasAdapted = planAdapted
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Advisor] FAILED for user {UserId}", userId);
                throw;
            }
        }
    }
}
