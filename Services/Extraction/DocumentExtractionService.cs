using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.AI.OpenAI;
using Azure;
using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Documents;
using BambooBrain_Service.Services.BlobStorage;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;

namespace BambooBrain_Service.Services.Extraction
{
    public class DocumentExtractionService : IExtractionService
    {
        private readonly DocumentAnalysisClient _docClient;
        private readonly OpenAIClient _openAiClient;
        private readonly IDocumentRepository _documents;
        private readonly IBlobStorageService _blob;
        private readonly IConfiguration _config;
        private readonly ILogger<DocumentExtractionService> _logger;

        public DocumentExtractionService(
            IDocumentRepository documents,
            IBlobStorageService blob,
            IConfiguration config,
            ILogger<DocumentExtractionService> logger)
        {
            _documents = documents;
            _blob = blob;
            _config = config;
            _logger = logger;

            _docClient = new DocumentAnalysisClient(
                new Uri(config["DocumentIntelligence:Endpoint"]!),
                new AzureKeyCredential(config["DocumentIntelligence:ApiKey"]!)
            );

            _openAiClient = new OpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!)
            );
        }

        public async Task ExtractAsync(Models.Document document)
        {
            try
            {
                _logger.LogInformation("Starting extraction for document {Id}", document.Id);

                // Step 1 — update status to analyzing
                await UpdateProgressAsync(document, "analyzing", 10);

                // Step 2 — download file from blob
                var container = "documents";
                var fileStream = await _blob.DownloadAsync(document.BlobPath, container);
                await UpdateProgressAsync(document, "analyzing", 20);

                // Step 3 — extract raw text using Document Intelligence
                var rawText = await ExtractTextAsync(fileStream, document.MimeType);
                await UpdateProgressAsync(document, "analyzing", 50);

                if (string.IsNullOrWhiteSpace(rawText))
                {
                    await UpdateStatusAsync(document, "failed");
                    return;
                }

                // Step 4 — extract Chinese words using GPT-4o
                var extractedWords = await ExtractChineseWordsAsync(rawText);
                await UpdateProgressAsync(document, "analyzing", 80);

                // Step 5 — determine HSK level from extracted words
                var hskLevel = DetermineHskLevel(extractedWords);

                // Step 6 — generate tags using GPT-4o
                var tags = await GenerateTagsAsync(rawText);
                await UpdateProgressAsync(document, "analyzing", 90);

                // Step 7 — save results
                document.ExtractedText = rawText.Length > 50000
                    ? rawText[..50000]  // cap at 50k chars to save storage
                    : rawText;
                document.ExtractedWords = extractedWords;
                document.HskLevel = hskLevel;
                document.Tags = tags;
                document.ExtractionStatus = "ready";
                document.ExtractionProgress = 100;
                document.UpdatedAt = DateTime.UtcNow;

                await _documents.UpdateAsync(document);
                _logger.LogInformation("Extraction complete for document {Id}", document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Extraction failed for document {Id}", document.Id);
                await UpdateStatusAsync(document, "failed");
            }
        }

        // ── Document Intelligence text extraction ──────────────────────────────

        private async Task<string> ExtractTextAsync(Stream fileStream, string mimeType)
        {
            // Use prebuilt-read model — best for text extraction across all document types
            var operation = await _docClient.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-read",
                fileStream,
                new AnalyzeDocumentOptions { Locale = "zh-Hans" } // Chinese simplified
            );

            var result = operation.Value;
            var sb = new StringBuilder();

            foreach (var page in result.Pages)
            {
                foreach (var line in page.Lines)
                {
                    sb.AppendLine(line.Content);
                }
            }

            return sb.ToString();
        }

        // ── GPT-4o Chinese word extraction ─────────────────────────────────────

        private async Task<List<ExtractedWord>> ExtractChineseWordsAsync(string rawText)
        {
            // Truncate text if too long for a single prompt
            var textSample = rawText.Length > 8000
                ? rawText[..8000]
                : rawText;

            var prompt = $$"""
                You are a Chinese language expert. Extract all unique Chinese vocabulary words from the text below.
    
                For each word, provide:
                - word: the Chinese characters
                - pinyin: pronunciation with tone marks (e.g. "Běijīng")
                - meaning: concise English meaning
                - hskLevel: HSK level 1-6 (null if unknown or not in HSK)
                - frequency: how many times it appears in the text
    
                Return ONLY a JSON array. No explanation, no markdown, no code blocks.
                Format: [{"word":"你好","pinyin":"nǐ hǎo","meaning":"hello","hskLevel":1,"frequency":3}]
    
                If there are no Chinese characters in the text, return an empty array: []
    
                TEXT:
                {{textSample}}
                """;

            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = _config["AzureOpenAI:DeploymentName"],
                Messages =
            {
                new ChatRequestSystemMessage(
                    "You are a Chinese language expert. Always respond with valid JSON only."),
                new ChatRequestUserMessage(prompt)
            },
                MaxTokens = 4000,
                Temperature = 0.1f  // low temperature for consistent structured output
            };

            var response = await _openAiClient.GetChatCompletionsAsync(chatOptions);
            var content = response.Value.Choices[0].Message.Content;

            try
            {
                var words = JsonSerializer.Deserialize<List<ExtractedWord>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return words ?? new List<ExtractedWord>();
            }
            catch
            {
                _logger.LogWarning("Failed to parse extracted words JSON: {Content}", content);
                return new List<ExtractedWord>();
            }
        }

        // ── GPT-4o tag generation ──────────────────────────────────────────────

        private async Task<List<string>> GenerateTagsAsync(string rawText)
        {
            var textSample = rawText.Length > 3000 ? rawText[..3000] : rawText;

            var prompt = $"""
            Based on this Chinese text, generate 3-6 relevant topic tags in English.
            Tags should reflect the main topics, themes, or context of the document.
            Examples: "travel", "business", "food", "culture", "daily conversation", "history"
            
            Return ONLY a JSON array of strings. No explanation.
            Example: ["travel", "culture", "Beijing"]
            
            TEXT:
            {textSample}
            """;

            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = _config["AzureOpenAI:DeploymentName"],
                Messages =
            {
                new ChatRequestSystemMessage(
                    "You are a helpful assistant. Always respond with valid JSON only."),
                new ChatRequestUserMessage(prompt)
            },
                MaxTokens = 200,
                Temperature = 0.3f
            };

            var response = await _openAiClient.GetChatCompletionsAsync(chatOptions);
            var content = response.Value.Choices[0].Message.Content;

            try
            {
                return JsonSerializer.Deserialize<List<string>>(content) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        // ── HSK level determination ────────────────────────────────────────────

        private int? DetermineHskLevel(List<ExtractedWord> words)
        {
            if (!words.Any()) return null;

            // Get the average HSK level of the top 20 most frequent words
            var hskWords = words
                .Where(w => w.HskLevel.HasValue)
                .OrderByDescending(w => w.Frequency)
                .Take(20)
                .ToList();

            if (!hskWords.Any()) return null;

            var avgLevel = hskWords.Average(w => w.HskLevel!.Value);
            return (int)Math.Round(avgLevel);
        }

        // ── Progress helpers ───────────────────────────────────────────────────

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
