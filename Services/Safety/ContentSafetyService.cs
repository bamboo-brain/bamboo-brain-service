using Azure;
using Azure.AI.ContentSafety;

namespace BambooBrain_Service.Services.Safety
{
    public class ContentSafetyService : IContentSafetyService
    {
        private readonly ContentSafetyClient _client;
        private readonly ILogger<ContentSafetyService> _logger;
        private const int BlockThreshold = 4; // 0-6 scale, block at medium+

        public ContentSafetyService(IConfiguration config,
            ILogger<ContentSafetyService> logger)
        {
            _logger = logger;
            _client = new ContentSafetyClient(
                new Uri(config["ContentSafety:Endpoint"]!),
                new AzureKeyCredential(config["ContentSafety:ApiKey"]!)
            );
        }

        public async Task<SafetyCheckResult> CheckTextAsync(string text)
        {
            try
            {
                var request = new AnalyzeTextOptions(text);
                var response = await _client.AnalyzeTextAsync(request);

                var hate = response.Value.CategoriesAnalysis
                    .FirstOrDefault(c => c.Category == TextCategory.Hate)?.Severity ?? 0;
                var selfHarm = response.Value.CategoriesAnalysis
                    .FirstOrDefault(c => c.Category == TextCategory.SelfHarm)?.Severity ?? 0;
                var sexual = response.Value.CategoriesAnalysis
                    .FirstOrDefault(c => c.Category == TextCategory.Sexual)?.Severity ?? 0;
                var violence = response.Value.CategoriesAnalysis
                    .FirstOrDefault(c => c.Category == TextCategory.Violence)?.Severity ?? 0;

                var maxScore = Math.Max(Math.Max(hate, selfHarm),
                                        Math.Max(sexual, violence));
                var isSafe = maxScore < BlockThreshold;

                if (!isSafe)
                    _logger.LogWarning(
                        "[Safety] Content blocked — hate:{H} selfharm:{S} sexual:{X} violence:{V}",
                        hate, selfHarm, sexual, violence);

                return new SafetyCheckResult
                {
                    IsSafe = isSafe,
                    BlockedReason = !isSafe ? GetBlockedReason(hate, selfHarm, sexual, violence) : null,
                    HateScore = hate,
                    SelfHarmScore = selfHarm,
                    SexualScore = sexual,
                    ViolenceScore = violence
                };
            }
            catch (Exception ex)
            {
                // Fail open with logging — don't block users if safety service is down
                _logger.LogError(ex, "[Safety] Content Safety check failed — allowing request");
                return new SafetyCheckResult { IsSafe = true };
            }
        }

        public async Task<bool> IsSafeAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            var result = await CheckTextAsync(text);
            return result.IsSafe;
        }

        private static string GetBlockedReason(int hate, int selfHarm,
            int sexual, int violence)
        {
            if (hate >= 4) return "Hate speech detected";
            if (selfHarm >= 4) return "Self-harm content detected";
            if (sexual >= 4) return "Sexual content detected";
            if (violence >= 4) return "Violence content detected";
            return "Unsafe content detected";
        }
    }
}
