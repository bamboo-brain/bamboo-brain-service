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

        public StudyAdvisorAgent(
            BambooBrainTools tools,
            IConfiguration config,
            ILogger<StudyAdvisorAgent> logger)
        {
            _tools = tools;
            _config = config;
            _logger = logger;
        }

        public async Task<StudyAdvisorResponse> ChatAsync(
            string userId, string message,
            List<AgentMessage>? history = null)
        {
            // Set userId on tools for this request
            _tools.UserId = userId;

            // Build Semantic Kernel with Azure OpenAI
            var kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: _config["AzureOpenAI:DeploymentName"]!,
                    endpoint: _config["AzureOpenAI:Endpoint"]!,
                    apiKey: _config["AzureOpenAI:ApiKey"]!
                )
                .Build();

            // Register tools as a plugin
            kernel.Plugins.AddFromObject(_tools, "BambooBrain");

            // Create the agent
            var agent = new ChatCompletionAgent
            {
                Name = "MasterLingAdvisor",
                Instructions = """
                You are Master Ling, an expert Chinese language study advisor for BambooBrain.
                You have access to tools that let you check the user's progress, search their
                documents, and modify their study plan.

                Your capabilities:
                - Check study statistics (streaks, vocabulary, quiz performance)
                - Review the active study plan and upcoming sessions
                - Search through uploaded documents for relevant content
                - Adapt the study plan when needed
                - Send notifications to keep the user informed
                - Calculate vocabulary gaps to target HSK level

                Behavior rules:
                - ALWAYS use tools to get real data before answering questions about progress
                - If the user asks about their progress, call get_user_stats first
                - If the user asks about their plan, call get_active_plan first
                - If the user mentions struggling, check stats AND plan before suggesting changes
                - If adaptation is needed, call adapt_study_plan and report what changed
                - After taking significant actions, send a notification to inform the user
                - Be encouraging, specific, and actionable
                - Respond in English unless the user writes in Chinese
                - Keep responses focused and concise — no more than 3-4 paragraphs
                """,
                Kernel = kernel,
                Arguments = new KernelArguments(
                    new AzureOpenAIPromptExecutionSettings
                    {
                        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                        MaxTokens = 1000,
                        Temperature = 0.4
                    })
            };

            // Build chat history
            var chatHistory = new ChatHistory();

            if (history != null)
            {
                foreach (var msg in history.TakeLast(6))
                {
                    if (msg.Role == "user")
                        chatHistory.AddUserMessage(msg.Content);
                    else
                        chatHistory.AddAssistantMessage(msg.Content);
                }
            }

            chatHistory.AddUserMessage(message);

            // Run the agent
            var actionsPerformed = new List<string>();
            var answer = string.Empty;
            var planAdapted = false;

            _logger.LogInformation(
                "[StudyAdvisor] Processing request for user {UserId}: {Message}",
                userId, message);

            await foreach (var response in agent.InvokeAsync(chatHistory))
            {
                if (response.Role == AuthorRole.Assistant)
                {
                    answer = response.Content ?? string.Empty;
                }
                else if (response.Role == AuthorRole.Tool)
                {
                    // Track which tools were called
                    var toolName = response.AuthorName ?? "unknown";
                    actionsPerformed.Add(toolName);

                    if (toolName.Contains("adapt_study_plan"))
                        planAdapted = true;

                    _logger.LogInformation(
                        "[StudyAdvisor] Tool called: {Tool}", toolName);
                }
            }

            return new StudyAdvisorResponse
            {
                Answer = answer,
                ActionsPerformed = actionsPerformed,
                PlanWasAdapted = planAdapted
            };
        }
    }
}
