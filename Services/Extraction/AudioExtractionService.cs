using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure;
using Azure.AI.Inference;
using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Documents;
using BambooBrain_Service.Services.BlobStorage;
using OpenAI.Chat;
using System.Text.Json;
using System.Text;

namespace BambooBrain_Service.Services.Extraction
{
    public class AudioExtractionService
    {
        private readonly ChatCompletionsClient _aiClient;
        private readonly IDocumentRepository _documents;
        private readonly IBlobStorageService _blob;
        private readonly IConfiguration _config;
        private readonly ILogger<AudioExtractionService> _logger;
        private readonly HttpClient _httpClient;

        // Speech batch transcription REST API base URL
        private string SpeechApiBase =>
            $"https://{_config["AzureSpeech:Region"]}.api.cognitive.microsoft.com/speechtotext/v3.2";

        public AudioExtractionService(
            IDocumentRepository documents,
            IBlobStorageService blob,
            IConfiguration config,
            ILogger<AudioExtractionService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _documents = documents;
            _blob = blob;
            _config = config;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("SpeechApi");
            _httpClient.DefaultRequestHeaders.Add(
                "Ocp-Apim-Subscription-Key", config["AzureSpeech:ApiKey"]);

            _aiClient = new ChatCompletionsClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!)
            );
        }

        public async Task ExtractAsync(Models.Document document)
        {
            string? transcriptionId = null;

            try
            {
                _logger.LogInformation("Starting batch audio extraction for {Id}", document.Id);
                await UpdateProgressAsync(document, "analyzing", 10);

                // Step 1 — Generate SAS URL for the audio blob
                // (Speech API needs a public URL to access the file)
                var sasUrl = await GenerateSasUrlAsync(document.BlobPath, "audios");
                await UpdateProgressAsync(document, "analyzing", 20);

                // Step 2 — Submit batch transcription job
                transcriptionId = await SubmitTranscriptionAsync(sasUrl, document.FileName);
                _logger.LogInformation("Transcription job submitted: {JobId}", transcriptionId);
                await UpdateProgressAsync(document, "analyzing", 30);

                // Step 3 — Poll until job completes
                var transcriptionResult = await PollTranscriptionAsync(transcriptionId, document);
                if (transcriptionResult == null)
                {
                    await UpdateStatusAsync(document, "failed");
                    return;
                }

                await UpdateProgressAsync(document, "analyzing", 60);

                // Step 4 — Extract Chinese words
                var extractedWords = await ExtractChineseWordsAsync(transcriptionResult.Transcript);
                await UpdateProgressAsync(document, "analyzing", 80);

                var hskLevel = DetermineHskLevel(extractedWords);
                var tags = await GenerateTagsAsync(transcriptionResult.Transcript);
                await UpdateProgressAsync(document, "analyzing", 90);

                // Step 5 — Save results
                document.ExtractedText = transcriptionResult.Transcript.Length > 50000
                    ? transcriptionResult.Transcript[..50000]
                    : transcriptionResult.Transcript;
                document.ExtractedWords = extractedWords;
                document.HskLevel = hskLevel;
                document.Tags = tags;
                document.Duration = transcriptionResult.Duration;
                document.ExtractionStatus = "ready";
                document.ExtractionProgress = 100;
                document.UpdatedAt = DateTime.UtcNow;

                await _documents.UpdateAsync(document);
                _logger.LogInformation("Audio extraction complete for {Id}", document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audio extraction failed for {Id}", document.Id);
                await UpdateStatusAsync(document, "failed");
            }
            finally
            {
                // Always clean up the transcription job from Azure
                if (transcriptionId != null)
                    await DeleteTranscriptionAsync(transcriptionId);
            }
        }

        // ── Step 1: Generate SAS URL ───────────────────────────────────────────

        private async Task<string> GenerateSasUrlAsync(string blobPath, string containerName)
        {
            var connectionString = _config["BlobStorage:ConnectionString"]!;
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            // Generate SAS valid for 2 hours — enough for batch transcription
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobPath,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(2)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }

        // ── Step 2: Submit transcription job ──────────────────────────────────

        private async Task<string> SubmitTranscriptionAsync(string sasUrl, string fileName)
        {
            var requestBody = new
            {
                contentUrls = new[] { sasUrl },
                locale = "zh-CN",
                displayName = $"transcription_{fileName}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                properties = new
                {
                    wordLevelTimestampsEnabled = false,
                    punctuationMode = "DictatedAndAutomatic",
                    profanityFilterMode = "Masked",
                    languageIdentification = new
                    {
                        // Auto-detect between Chinese and English
                        candidateLocales = new[] { "zh-CN", "en-US" }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{SpeechApiBase}/transcriptions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to submit transcription: {response.StatusCode} {responseBody}");

            var result = JsonSerializer.Deserialize<TranscriptionResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Extract ID from the self URL
            // e.g. https://region.api.cognitive.microsoft.com/speechtotext/v3.2/transcriptions/abc123
            var selfUrl = result?.Self ?? throw new Exception("No self URL in transcription response");
            return selfUrl.Split('/').Last();
        }

        // ── Step 3: Poll until complete ────────────────────────────────────────

        private async Task<TranscriptResult?> PollTranscriptionAsync(
            string transcriptionId, Models.Document document)
        {
            var maxWaitMinutes = 30;
            var pollInterval = TimeSpan.FromSeconds(15);
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < TimeSpan.FromMinutes(maxWaitMinutes))
            {
                await Task.Delay(pollInterval);

                var response = await _httpClient.GetAsync(
                    $"{SpeechApiBase}/transcriptions/{transcriptionId}");
                var body = await response.Content.ReadAsStringAsync();

                var status = JsonSerializer.Deserialize<TranscriptionStatusResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger.LogInformation("Transcription {Id} status: {Status}",
                    transcriptionId, status?.Status);

                switch (status?.Status?.ToLower())
                {
                    case "succeeded":
                        // Get the actual transcript content
                        return await GetTranscriptContentAsync(transcriptionId);

                    case "failed":
                        _logger.LogError("Transcription failed: {Error}", status.Properties?.Error);
                        return null;

                    case "running":
                        // Update progress incrementally while waiting
                        var elapsed = (DateTime.UtcNow - startTime).TotalMinutes;
                        var progress = Math.Min(30 + (int)(elapsed / maxWaitMinutes * 30), 58);
                        await UpdateProgressAsync(document, "analyzing", progress);
                        break;

                        // "notstarted" | "preparing" → keep polling
                }
            }

            _logger.LogError("Transcription timed out after {Minutes} minutes", maxWaitMinutes);
            return null;
        }

        // ── Step 3b: Get transcript content after success ──────────────────────

        private async Task<TranscriptResult?> GetTranscriptContentAsync(string transcriptionId)
        {
            // Get list of result files
            var response = await _httpClient.GetAsync(
                $"{SpeechApiBase}/transcriptions/{transcriptionId}/files");
            var body = await response.Content.ReadAsStringAsync();

            var files = JsonSerializer.Deserialize<TranscriptionFilesResponse>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Find the transcript result file
            var transcriptFile = files?.Values?
                .FirstOrDefault(f => f.Kind == "Transcription");

            if (transcriptFile?.Links?.ContentUrl == null)
            {
                _logger.LogError("No transcript file found");
                return null;
            }

            // Download the actual transcript JSON
            var transcriptResponse = await _httpClient.GetAsync(
                transcriptFile.Links.ContentUrl);
            var transcriptBody = await transcriptResponse.Content.ReadAsStringAsync();

            var transcript = JsonSerializer.Deserialize<TranscriptContent>(transcriptBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Combine all recognized phrases into one text
            var sb = new StringBuilder();
            var totalDuration = TimeSpan.Zero;

            if (transcript?.CombinedRecognizedPhrases != null)
            {
                foreach (var phrase in transcript.CombinedRecognizedPhrases)
                {
                    sb.AppendLine(phrase.Display);
                }
            }

            if (transcript?.RecognizedPhrases != null && transcript.RecognizedPhrases.Any())
            {
                var lastPhrase = transcript.RecognizedPhrases.Last();
                // Parse duration from ISO 8601 format e.g. "PT1M30.5S"
                if (TimeSpan.TryParse(lastPhrase.Offset, out var offset) &&
                    TimeSpan.TryParse(lastPhrase.Duration, out var duration))
                {
                    totalDuration = offset + duration;
                }
            }

            return new TranscriptResult
            {
                Transcript = sb.ToString().Trim(),
                Duration = FormatDuration(totalDuration)
            };
        }

        // ── Step 4: Delete job after completion ────────────────────────────────

        private async Task DeleteTranscriptionAsync(string transcriptionId)
        {
            try
            {
                await _httpClient.DeleteAsync(
                    $"{SpeechApiBase}/transcriptions/{transcriptionId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to delete transcription job {Id}: {Error}",
                    transcriptionId, ex.Message);
            }
        }

        // ── GPT word extraction ────────────────────────────────────────────────

        private async Task<List<ExtractedWord>> ExtractChineseWordsAsync(string rawText)
        {
            var textSample = rawText.Length > 8000 ? rawText[..8000] : rawText;

            var requestOptions = new ChatCompletionsOptions
            {
                Model = _config["AzureOpenAI:DeploymentName"],
                Messages =
            {
                new ChatRequestSystemMessage(
                    "You are a Chinese language expert. Always respond with valid JSON only."),
                new ChatRequestUserMessage($$"""
                    Extract all unique Chinese vocabulary words from the text below.
                    For each word: word (Chinese chars), pinyin (tone marks), meaning (English), hskLevel (1-6 or null), frequency (count).
                    Return ONLY a JSON array. No markdown.
                    Example: [{"word":"你好","pinyin":"nǐ hǎo","meaning":"hello","hskLevel":1,"frequency":3}]
                    If no Chinese: []
                    
                    TEXT: {{textSample}}
                    """)
            },
                MaxTokens = 4000,
                Temperature = 0.1f
            };

            var response = await _aiClient.CompleteAsync(requestOptions);
            var content = response.Value.Content
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

            var requestOptions = new ChatCompletionsOptions
            {
                Model = _config["AzureOpenAI:DeploymentName"],
                Messages =
            {
                new ChatRequestSystemMessage(
                    "You are a helpful assistant. Always respond with valid JSON only."),
                new ChatRequestUserMessage($$"""
                    Generate 3-6 topic tags in English for this Chinese text.
                    Return ONLY a JSON array. Example: ["travel","culture","Beijing"]
                    
                    TEXT: {{textSample}}
                    """)
            },
                MaxTokens = 200,
                Temperature = 0.3f
            };

            var response = await _aiClient.CompleteAsync(requestOptions);
            var content = response.Value.Content
                .Replace("```json", "").Replace("```", "").Trim();

            try { return JsonSerializer.Deserialize<List<string>>(content) ?? new(); }
            catch { return new(); }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

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

        private static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                : $"{duration.Minutes}:{duration.Seconds:D2}";
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
