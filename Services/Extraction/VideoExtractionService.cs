using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Documents;
using BambooBrain_Service.Services.BlobStorage;
using System.Text.Json;
using System.Text;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;
using Azure.Core;
using System.Net.Http.Headers;
using Azure.Identity;

namespace BambooBrain_Service.Services.Extraction
{
    public class VideoExtractionService
    {
        private readonly AzureOpenAIClient _aiClient;
        private readonly IDocumentRepository _documents;
        private readonly IBlobStorageService _blob;
        private readonly IConfiguration _config;
        private readonly ILogger<VideoExtractionService> _logger;
        private readonly HttpClient _httpClient;

        private string VideoIndexerApiBase =>
            $"https://api.videoindexer.ai/{_config["VideoIndexer:Location"]}/Accounts/{_config["VideoIndexer:AccountId"]}";

        public VideoExtractionService(
            IDocumentRepository documents,
            IBlobStorageService blob,
            IConfiguration config,
            ILogger<VideoExtractionService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _documents = documents;
            _blob = blob;
            _config = config;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("VideoIndexer");

            _aiClient = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!),
                new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            );
        }

        public async Task ExtractAsync(Models.Document document)
        {
            string? videoId = null;

            try
            {
                _logger.LogInformation("Starting video extraction for {Id}", document.Id);
                await UpdateProgressAsync(document, "analyzing", 10);

                // Step 1 — Get access token
                var accessToken = await GetAccessTokenAsync();
                await UpdateProgressAsync(document, "analyzing", 15);

                // Step 2 — Generate SAS URL for video blob
                var sasUrl = await GenerateSasUrlAsync(document.BlobPath, "videos");
                await UpdateProgressAsync(document, "analyzing", 20);

                // Step 3 — Upload video to Video Indexer
                videoId = await UploadVideoAsync(sasUrl, document.FileName, accessToken);
                _logger.LogInformation("Video uploaded to indexer: {VideoId}", videoId);
                await UpdateProgressAsync(document, "analyzing", 30);

                // Step 4 — Poll until indexing complete
                var result = await PollVideoIndexingAsync(videoId, accessToken, document);
                if (result == null)
                {
                    await UpdateStatusAsync(document, "failed");
                    return;
                }

                await UpdateProgressAsync(document, "analyzing", 65);

                // Step 5 — Extract Chinese words from transcript
                var extractedWords = await ExtractChineseWordsAsync(result.Transcript);
                await UpdateProgressAsync(document, "analyzing", 80);

                // Map timestamps to words
                MapTimestampsToWords(extractedWords, result.WordTimings);

                var hskLevel = DetermineHskLevel(extractedWords);
                var tags = await GenerateTagsAsync(result.Transcript);
                await UpdateProgressAsync(document, "analyzing", 90);

                // Step 6 — Save results
                document.ExtractedText = result.Transcript.Length > 50000
                    ? result.Transcript[..50000]
                    : result.Transcript;
                document.ExtractedWords = extractedWords;
                document.HskLevel = hskLevel;
                document.Tags = tags;
                document.Duration = result.Duration;
                document.ExtractionStatus = "ready";
                document.ExtractionProgress = 100;
                document.UpdatedAt = DateTime.UtcNow;

                await _documents.UpdateAsync(document);
                _logger.LogInformation("Video extraction complete for {Id}", document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Video extraction failed for {Id}", document.Id);
                await UpdateStatusAsync(document, "failed");
            }
            finally
            {
                // Clean up video from indexer to save storage quota
                if (videoId != null)
                    await DeleteVideoAsync(videoId);
            }
        }

        // ── Step 1: Get access token ───────────────────────────────────────────
        private async Task<string> GetAccessTokenAsync()
        {
            var options = new DefaultAzureCredentialOptions
            {
                TenantId = _config["VideoIndexer:TenantId"]
            };

            var credential = new DefaultAzureCredential(options);
            var armTokenResult = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }));

            var url = $"https://management.azure.com/subscriptions/{_config["VideoIndexer:SubscriptionId"]}" +
                      $"/resourceGroups/{_config["VideoIndexer:ResourceGroupName"]}" +
                      $"/providers/Microsoft.VideoIndexer/accounts/{_config["VideoIndexer:AccountName"]}" +
                      $"/generateAccessToken?api-version=2024-01-01";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", armTokenResult.Token);

            var requestBody = new { permissionType = "Contributor", scope = "Account" };
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to generate ARM access token: {body}");
            }

            using var jsonDoc = JsonDocument.Parse(body);
            if (jsonDoc.RootElement.TryGetProperty("accessToken", out var tokenElement))
            {
                return tokenElement.GetString() ?? throw new Exception("Token was null");
            }

            throw new Exception("Could not find 'accessToken' property in the response.");
        }

        // ── Step 2: Upload video ───────────────────────────────────────────────

        private async Task<string> UploadVideoAsync(
            string sasUrl, string fileName, string accessToken)
        {
            var encodedUrl = Uri.EscapeDataString(sasUrl);
            var encodedName = Uri.EscapeDataString(Path.GetFileNameWithoutExtension(fileName));

            var url = $"{VideoIndexerApiBase}/Videos" +
                  $"?accessToken={Uri.EscapeDataString(accessToken)}" +
                  $"&name={encodedName}" +
                  $"&videoUrl={encodedUrl}" +
                  $"&language=zh-Hans" +
                  $"&indexingPreset=Default" +
                  $"&streamingPreset=NoStreaming";

            var response = await _httpClient.PostAsync(url, null);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to upload video: {response.StatusCode} {body}");

            var result = JsonSerializer.Deserialize<VideoIndexerUploadResponse>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result?.Id ?? throw new Exception("No video ID returned");
        }

        // ── Step 3: Poll until complete ────────────────────────────────────────

        private async Task<VideoTranscriptResult?> PollVideoIndexingAsync(
            string videoId, string accessToken, Models.Document document)
        {
            var maxWaitMinutes = 60;
            var pollInterval = TimeSpan.FromSeconds(20);
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < TimeSpan.FromMinutes(maxWaitMinutes))
            {
                await Task.Delay(pollInterval);

                var url = $"{VideoIndexerApiBase}/Videos/{videoId}/Index" +
                          $"?accessToken={accessToken}&language=zh-Hans";

                var response = await _httpClient.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                var status = JsonSerializer.Deserialize<VideoIndexerStatusResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger.LogInformation("Video {VideoId} state: {State}",
                    videoId, status?.State);

                switch (status?.State?.ToLower())
                {
                    case "processed":
                        return ParseVideoInsights(status);

                    case "failed":
                        _logger.LogError("Video indexing failed for {VideoId}", videoId);
                        return null;

                    default:
                        // Update progress while waiting
                        var elapsed = (DateTime.UtcNow - startTime).TotalMinutes;
                        var progress = Math.Min(30 + (int)(elapsed / maxWaitMinutes * 35), 63);
                        await UpdateProgressAsync(document, "analyzing", progress);
                        break;
                }
            }

            _logger.LogError("Video indexing timed out after {Minutes} minutes", maxWaitMinutes);
            return null;
        }

        // ── Step 4: Parse insights ─────────────────────────────────────────────

        private VideoTranscriptResult ParseVideoInsights(VideoIndexerStatusResponse status)
        {
            var insights = status.Videos?.FirstOrDefault()?.Insights;
            if (insights == null)
                return new VideoTranscriptResult { Transcript = string.Empty };

            var sb = new StringBuilder();
            var wordTimings = new List<WordTiming>();

            // Build transcript from transcript segments
            if (insights.Transcript != null)
            {
                foreach (var segment in insights.Transcript)
                {
                    if (string.IsNullOrEmpty(segment.Text)) continue;
                    sb.AppendLine(segment.Text);

                    // Extract word timings from each segment
                    if (segment.Instances?.Any() == true)
                    {
                        var words = segment.Text.Split(' ',
                            StringSplitOptions.RemoveEmptyEntries);
                        var instance = segment.Instances.First();

                        if (TimeSpan.TryParse(instance.Start, out var startTime) &&
                            TimeSpan.TryParse(instance.End, out var endTime))
                        {
                            var segmentDuration = (endTime - startTime).TotalSeconds;
                            var timePerWord = segmentDuration / Math.Max(words.Length, 1);

                            for (int i = 0; i < words.Length; i++)
                            {
                                wordTimings.Add(new WordTiming
                                {
                                    Word = words[i],
                                    Offset = TimeSpan.FromSeconds(
                                        startTime.TotalSeconds + i * timePerWord)
                                        .ToString(@"hh\:mm\:ss\.fff"),
                                    Duration = TimeSpan.FromSeconds(timePerWord)
                                        .ToString(@"hh\:mm\:ss\.fff")
                                });
                            }
                        }
                    }
                }
            }

            return new VideoTranscriptResult
            {
                Transcript = sb.ToString().Trim(),
                Duration = insights.Duration,
                WordTimings = wordTimings
            };
        }

        // ── Step 5: Delete video from indexer ─────────────────────────────────

        private async Task DeleteVideoAsync(string videoId)
        {
            try
            {
                var accessToken = await GetAccessTokenAsync();
                await _httpClient.DeleteAsync(
                    $"{VideoIndexerApiBase}/Videos/{videoId}?accessToken={accessToken}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to delete video {VideoId}: {Error}",
                    videoId, ex.Message);
            }
        }

        // ── SAS URL generation ─────────────────────────────────────────────────

        private async Task<string> GenerateSasUrlAsync(string blobPath, string containerName)
        {
            return await _blob.GenerateSasUrlAsync(blobPath, containerName, expiryMinutes: 120);
        }

        // ── GPT extraction (reuse shared service) ─────────────────────────────

        private async Task<List<ExtractedWord>> ExtractChineseWordsAsync(string rawText)
        {
            var textSample = rawText.Length > 8000 ? rawText[..8000] : rawText;

            var prompt = $$"""
                Extract all unique Chinese vocabulary words from the text below.
                For each: word (chars), pinyin (tone marks), meaning (English), hskLevel (1-6 or null), frequency (count).
                Return ONLY a JSON array. No markdown.
                Example: [{"word":"你好","pinyin":"nǐ hǎo","meaning":"hello","hskLevel":1,"frequency":3}]
                If no Chinese: []

                TEXT: {{textSample}}
                """;

            var chatClient = _aiClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage("You are a Chinese language expert. Always respond with valid JSON only."),
                    new UserChatMessage(prompt)
                        },
                        new ChatCompletionOptions
                        {
                            MaxOutputTokenCount = 4000,
                            Temperature = 0.1f
                        }
            );

            var content = response.Value.Content[0].Text
                .Replace("```json", "").Replace("```", "").Trim();

            try
            {
                return JsonSerializer.Deserialize<List<ExtractedWord>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to parse words: {Error}", ex.Message);
                return new();
            }
        }

        private async Task<List<string>> GenerateTagsAsync(string rawText)
        {
            var textSample = rawText.Length > 3000 ? rawText[..3000] : rawText;

            var prompt = $$"""
                    Generate 3-6 topic tags in English for this Chinese text.
                    Return ONLY a JSON array. Example: ["travel","culture","Beijing"]
                    
                    TEXT: {{textSample}}
                    """;

            var chatClient = _aiClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage("You are a helpful assistant. Always respond with valid JSON only."),
                    new UserChatMessage(prompt)
                        },
                        new ChatCompletionOptions
                        {
                            MaxOutputTokenCount = 200,
                            Temperature = 0.3f
                        }
            );

            var content = response.Value.Content[0].Text
                .Replace("```json", "").Replace("```", "").Trim();


            try { return JsonSerializer.Deserialize<List<string>>(content) ?? new(); }
            catch { return new(); }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void MapTimestampsToWords(
            List<ExtractedWord> extractedWords,
            List<WordTiming> wordTimings)
        {
            foreach (var extracted in extractedWords)
            {
                var timing = wordTimings.FirstOrDefault(w =>
                    w.Word != null &&
                    (w.Word.Contains(extracted.Word) || extracted.Word.Contains(w.Word)));

                if (timing != null)
                {
                    extracted.OffsetSeconds = ParseTimespan(timing.Offset).TotalSeconds;
                    extracted.DurationSeconds = ParseTimespan(timing.Duration).TotalSeconds;
                }
            }
        }

        private static TimeSpan ParseTimespan(string? value)
        {
            if (string.IsNullOrEmpty(value)) return TimeSpan.Zero;
            return TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero;
        }

        private int? DetermineHskLevel(List<ExtractedWord> words)
        {
            if (!words.Any()) return null;
            var hskWords = words
                .Where(w => w.HskLevel.HasValue)
                .OrderByDescending(w => w.Frequency)
                .Take(20).ToList();
            if (!hskWords.Any()) return null;
            return (int)Math.Round(hskWords.Average(w => w.HskLevel!.Value));
        }

        private async Task UpdateProgressAsync(Models.Document document, string status, int progress)
        {
            document.ExtractionStatus = status;
            document.ExtractionProgress = progress;
            document.UpdatedAt = DateTime.UtcNow;
            await _documents.UpdateAsync(document);
        }

        private async Task UpdateStatusAsync(Models.Document document, string status)
        {
            document.ExtractionStatus = status;
            document.UpdatedAt = DateTime.UtcNow;
            await _documents.UpdateAsync(document);
        }
    }

}
