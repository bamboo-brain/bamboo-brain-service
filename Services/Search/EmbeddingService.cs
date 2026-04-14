using Azure.AI.OpenAI;
using Azure;

namespace BambooBrain_Service.Services.Search
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly AzureOpenAIClient _client;
        private readonly string _deploymentName;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(IConfiguration config, ILogger<EmbeddingService> logger)
        {
            _logger = logger;
            _client = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!)
            );
            _deploymentName = config["AzureOpenAI:EmbeddingDeployment"]
                ?? "text-embedding-ada-002";
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
                var embeddingClient = _client.GetEmbeddingClient(_deploymentName);
                var response = await embeddingClient.GenerateEmbeddingAsync(text);
                return response.Value.ToFloats().ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Embedding failed for text: {Text}",
                    text[..Math.Min(50, text.Length)]);
                return Array.Empty<float>();
            }
        }

        public async Task<List<float[]>> GetEmbeddingsBatchAsync(List<string> texts)
        {
            var results = new List<float[]>();
            // Process in batches of 16 to avoid rate limits
            foreach (var batch in texts.Chunk(16))
            {
                var tasks = batch.Select(t => GetEmbeddingAsync(t));
                var batchResults = await Task.WhenAll(tasks);
                results.AddRange(batchResults);
                // Small delay to avoid throttling
                await Task.Delay(100);
            }
            return results;
        }
    }
}
