using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace BambooBrain_Service.Services.Agents
{
    public class StudyAdvisorAgent : IStudyAdvisorAgent
    {
        private readonly BambooBrainTools _tools;
        private readonly IConfiguration _config;
        private readonly ILogger<StudyAdvisorAgent> _logger;
        private readonly FoundryChatCompletionService _chatService;

        public StudyAdvisorAgent(
            BambooBrainTools tools,
            FoundryChatCompletionService chatService,
            IConfiguration config,
            ILogger<StudyAdvisorAgent> logger)
        {
            _tools = tools;
            _chatService = chatService;
            _config = config;
            _logger = logger;
        }

        public async Task<StudyAdvisorResponse> ChatAsync(string userId, string message, List<AgentMessage>? history = null)
        {
            _tools.UserId = userId;

            try
            {
                _logger.LogInformation(
                    "[Advisor] Building kernel for user {UserId}", userId);

                // ← Use FoundryChatCompletionService instead of AddAzureOpenAIChatCompletion
                var builder = Kernel.CreateBuilder();
                builder.Services.AddSingleton<IChatCompletionService>(_chatService);
                var kernel = builder.Build();

                // Register tools
                kernel.Plugins.AddFromObject(_tools, "BambooBrain");

                _logger.LogInformation("[Advisor] Creating agent...");

                var agent = new ChatCompletionAgent
                {
                    Name = "MasterLingAdvisor",
                    Instructions = """
                    You are Master Ling, an expert Chinese language study advisor
                    for BambooBrain.

                    You have access to tools that give you real data about the user:
                    - get_user_stats: streak, vocabulary, quiz scores, study time
                    - get_active_plan: upcoming sessions, skipped events, goals
                    - search_user_documents: search their uploaded study materials
                    - adapt_study_plan: modify the plan when adjustment is needed
                    - send_notification: notify the user about important actions
                    - get_vocabulary_gap: calculate words needed for target HSK level

                    Rules:
                    - ALWAYS call tools to get real data before answering
                    - If asked about progress, call get_user_stats first
                    - If asked about schedule, call get_active_plan first
                    - If the user is struggling, check both stats and plan
                    - Be encouraging, specific, and concise
                    - Respond in English unless the user writes in Chinese
                    """,
                    Kernel = kernel,
                    Arguments = new KernelArguments(
                        new OpenAIPromptExecutionSettings
                        {
                            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                            MaxTokens = 1000,
                            Temperature = 0.4
                        })
                };

                // Build chat history
                var chatHistory = new ChatHistory();
                if (history != null)
                    foreach (var msg in history.TakeLast(6))
                    {
                        if (msg.Role == "user")
                            chatHistory.AddUserMessage(msg.Content);
                        else
                            chatHistory.AddAssistantMessage(msg.Content);
                    }
                chatHistory.AddUserMessage(message);

                _logger.LogInformation("[Advisor] Invoking agent...");

                var answer = string.Empty;
                var actionsPerformed = new List<string>();
                var planAdapted = false;

                await foreach (var response in agent.InvokeAsync(chatHistory))
                {
                    if (response.Role == AuthorRole.Assistant &&
                        !string.IsNullOrEmpty(response.Content))
                    {
                        answer = response.Content;
                        _logger.LogInformation(
                            "[Advisor] Got answer: {Len} chars", answer.Length);
                    }
                }

                var toolCalls = chatHistory
                    .Where(m => m.Role == AuthorRole.Tool)
                    .Select(m => m.Content ?? "tool_called")
                    .ToList();

                actionsPerformed = toolCalls;
                planAdapted = toolCalls.Any(t =>
                    t.Contains("adapt_study_plan") ||
                    t.Contains("planAdapted"));

                if (string.IsNullOrEmpty(answer))
                    answer = "I wasn't able to generate a response. Please try again.";

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
