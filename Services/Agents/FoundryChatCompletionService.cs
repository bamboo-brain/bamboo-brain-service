using Azure;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

// ← Alias OpenAI types to avoid conflict with SK types
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;
using OpenAISystemMessage = OpenAI.Chat.SystemChatMessage;
using OpenAIAssistantMessage = OpenAI.Chat.AssistantChatMessage;
using OpenAIUserMessage = OpenAI.Chat.UserChatMessage;
using OpenAIChatOptions = OpenAI.Chat.ChatCompletionOptions;

// ← Explicitly use SK type for the return value
using SKChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace BambooBrain_Service.Services.Agents
{
    public class FoundryChatCompletionService : IChatCompletionService
    {
        private readonly AzureOpenAIClient _client;
        private readonly string _deploymentName;
        private readonly ILogger<FoundryChatCompletionService> _logger;

        public IReadOnlyDictionary<string, object?> Attributes =>
            new Dictionary<string, object?>();

        public FoundryChatCompletionService(
            IConfiguration config,
            ILogger<FoundryChatCompletionService> logger)
        {
            _logger = logger;
            _deploymentName = config["AzureOpenAI:DeploymentName"]!;

            // ← Same pattern as AudioExtractionService
            _client = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!),
                new AzureOpenAIClientOptions(
                    AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            );
        }

        public async Task<IReadOnlyList<SKChatMessageContent>> GetChatMessageContentsAsync(
             ChatHistory chatHistory,
             PromptExecutionSettings? executionSettings = null,
             Kernel? kernel = null,
             CancellationToken cancellationToken = default)
        {
            var chatClient = _client.GetChatClient(_deploymentName);

            // ← Use aliased OpenAI types — no ambiguity
            var messages = new List<OpenAIChatMessage>();
            foreach (var m in chatHistory)
            {
                if (m.Role == AuthorRole.System)
                    messages.Add(new OpenAISystemMessage(m.Content ?? ""));
                else if (m.Role == AuthorRole.Assistant)
                    messages.Add(new OpenAIAssistantMessage(m.Content ?? ""));
                else
                    messages.Add(new OpenAIUserMessage(m.Content ?? ""));
            }

            _logger.LogInformation(
                "[FoundryChat] Sending {Count} messages to {Model}",
                messages.Count, _deploymentName);

            var response = await chatClient.CompleteChatAsync(
                messages,
                new OpenAIChatOptions
                {
                    MaxOutputTokenCount = 1500,
                    Temperature = 0.4f
                },
                cancellationToken);

            var content = response.Value.Content[0].Text;

            _logger.LogInformation(
                "[FoundryChat] Response: {Len} chars", content?.Length ?? 0);

            // ← Return SK type explicitly — no ambiguity
            return new List<SKChatMessageContent>
            {
                new SKChatMessageContent(AuthorRole.Assistant, content)
            };
        }

        public IAsyncEnumerable<StreamingChatMessageContent>
        GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    }
}
