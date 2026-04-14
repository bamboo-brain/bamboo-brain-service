using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;

namespace BambooBrain_Service.Services.Search
{
    public class RagChatService : IRagChatService
    {
        private readonly IAISearchService _search;
        private readonly AzureOpenAIClient _aiClient;
        private readonly IConfiguration _config;
        private readonly ILogger<RagChatService> _logger;

        public RagChatService(
            IAISearchService search,
            IConfiguration config,
            ILogger<RagChatService> logger)
        {
            _search = search;
            _config = config;
            _logger = logger;

            _aiClient = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!)
            );
        }

        public async Task<RagChatResponse> ChatAsync(
            string userId, string question,
            List<RagChatMessage>? history = null)
        {
            // Check if question mentions a specific document title
            string? documentTitleHint = null;
            if (question.Contains(".pdf") || question.Contains(".mp4") ||
                question.Contains(".mp3"))
            {
                // Extract filename from question
                var words = question.Split(' ');
                documentTitleHint = words.FirstOrDefault(w =>
                    w.Contains(".pdf") || w.Contains(".mp4") || w.Contains(".mp3"));
            }

            // Step 1 — retrieve relevant chunks
            var ragResult = await _search.SearchChunksForRagAsync(
                userId, question, topChunks: 5,
                documentTitleHint: documentTitleHint);  // ← pass hint

            var hasContext = ragResult.Chunks.Any();

            // Step 2 — build messages for GPT-4o
            var messages = new List<ChatMessage>();

            var systemPrompt = hasContext
                ? $$"""
                You are a helpful Chinese language learning assistant for BambooBrain.
                Answer the user's question based on the provided context from their
                uploaded documents. If the answer is not in the context, say so clearly.

                Context from user's documents:
                {{ragResult.CombinedContext}}

                Rules:
                - Answer in English unless the user writes in Chinese
                - Quote relevant Chinese words with their pinyin when helpful
                - Be concise and educational
                - If asked about vocabulary, provide pinyin and meaning
                - Cite which document the information comes from
                """
                : """
                You are a helpful Chinese language learning assistant for BambooBrain.
                The user hasn't uploaded any relevant documents yet, or no relevant
                content was found. Answer based on your general knowledge of Chinese,
                and suggest they upload relevant study materials for better assistance.
                """;

            messages.Add(new SystemChatMessage(systemPrompt));

            // Add conversation history
            if (history != null)
            {
                foreach (var msg in history.TakeLast(6))
                {
                    if (msg.Role == "user")
                        messages.Add(new UserChatMessage(msg.Content));
                    else
                        messages.Add(new AssistantChatMessage(msg.Content));
                }
            }

            messages.Add(new UserChatMessage(question));

            // Step 3 — call GPT-4o
            var chatClient = _aiClient.GetChatClient(
                _config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(messages,
                new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 1000,
                    Temperature = 0.3f
                });

            var answer = response.Value.Content[0].Text;

            return new RagChatResponse
            {
                Answer = answer,
                Sources = ragResult.Chunks.Take(3).ToList(),
                HasSources = hasContext
            };
        }
    }
}
