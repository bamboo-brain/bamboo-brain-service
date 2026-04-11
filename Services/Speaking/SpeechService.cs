using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace BambooBrain_Service.Services.Speaking
{
    public class SpeechService : ISpeechService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SpeechService> _logger;

        private string SpeechRegion => _config["AzureSpeech:Region"]!;
        private string SpeechKey => _config["AzureSpeech:ApiKey"]!;
        private string BlobConnectionString => _config["BlobStorage:ConnectionString"]!;

        public SpeechService(IConfiguration config, ILogger<SpeechService> logger)
        {
            _config = config;
            _logger = logger;
        }

        // ── STT ────────────────────────────────────────────────────────────────

        public async Task<SpeechRecognitionResult> RecognizeAsync(string audioBase64, string mimeType)
        {
            try
            {
                var audioBytes = Convert.FromBase64String(audioBase64);

                // ← Choose correct codec based on MIME type
                var (contentType, codecParam) = mimeType.ToLower() switch
                {
                    "audio/webm" or "audio/webm;codecs=opus" =>
                        ("audio/webm;codecs=opus", "&format=detailed"),
                    "audio/ogg" or "audio/ogg;codecs=opus" =>
                        ("audio/ogg;codecs=opus", "&format=detailed"),
                    _ =>
                        ("audio/wav", "&format=detailed")
                };

                var url = $"https://{SpeechRegion}.stt.speech.microsoft.com/" +
                          $"speech/recognition/conversation/cognitiveservices/v1" +
                          $"?language=zh-CN{codecParam}";

                using var httpClient = new HttpClient();
                using var content = new ByteArrayContent(audioBytes);
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };
                request.Headers.Add("Ocp-Apim-Subscription-Key", SpeechKey);

                var response = await httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("STT response: {Status} {Body}",
                    response.StatusCode, body[..Math.Min(300, body.Length)]);

                if (!response.IsSuccessStatusCode)
                    return new SpeechRecognitionResult
                    {
                        Success = false,
                        Error = $"STT failed: {response.StatusCode} {body}"
                    };

                var result = System.Text.Json.JsonSerializer.Deserialize<SttResponse>(
                    body, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (result?.RecognitionStatus != "Success" ||
                    result.NBest == null || !result.NBest.Any() ||
                    string.IsNullOrWhiteSpace(result.NBest.First().Display))
                    return new SpeechRecognitionResult
                    {
                        Success = false,
                        Error = $"No speech recognized. Status: {result?.RecognitionStatus}"
                    };

                var best = result.NBest.First();
                return new SpeechRecognitionResult
                {
                    Success = true,
                    Text = best.Display ?? best.Lexical ?? string.Empty,
                    AccuracyScore = best.Confidence * 100
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "STT exception");
                return new SpeechRecognitionResult { Success = false, Error = ex.Message };
            }
        }

        // ── TTS ────────────────────────────────────────────────────────────────
        public async Task<string> SynthesizeAsync(string text, string userId, string sessionId)
        {
            _logger.LogInformation("[TTS] Starting. Region={Region} Text={Text}",
                SpeechRegion, text);

            var url = $"https://{SpeechRegion}.tts.speech.microsoft.com/cognitiveservices/v1";

            var ssml = $"<speak version='1.0' xml:lang='zh-CN'>" +
                       $"<voice name='zh-CN-XiaoxiaoNeural'>" +
                       $"{System.Security.SecurityElement.Escape(text)}" +
                       $"</voice></speak>";

            _logger.LogInformation("[TTS] SSML: {Ssml}", ssml);

            // ← Use ByteArrayContent so we control Content-Type exactly — no charset suffix
            var ssmlBytes = System.Text.Encoding.UTF8.GetBytes(ssml);

            using var httpClient = new HttpClient();
            using var content = new ByteArrayContent(ssmlBytes);
            content.Headers.TryAddWithoutValidation(
                "Content-Type", "application/ssml+xml");

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.TryAddWithoutValidation(
                "Ocp-Apim-Subscription-Key", SpeechKey);
            request.Headers.TryAddWithoutValidation(
                "X-Microsoft-OutputFormat", "audio-16khz-128kbitrate-mono-mp3");
            request.Headers.TryAddWithoutValidation(
                "User-Agent", "BambooBrain");

            _logger.LogInformation("[TTS] Sending request to {Url}", url);
            var response = await httpClient.SendAsync(request);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("[TTS] Status: {Status} Body: {Body}",
                response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
                return string.Empty;

            var audioBytes = response.Content.ReadAsByteArrayAsync().Result;
            _logger.LogInformation("[TTS] Audio bytes: {Length}", audioBytes.Length);

            if (audioBytes.Length == 0)
            {
                _logger.LogError("[TTS] Empty audio response");
                return string.Empty;
            }

            // Upload to blob
            var blobServiceClient = new BlobServiceClient(BlobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient("speaking-audio");
            await containerClient.CreateIfNotExistsAsync();

            var blobName = $"{userId}/{sessionId}/{Guid.NewGuid()}.mp3";
            var blobClient = containerClient.GetBlobClient(blobName);

            using var stream = new MemoryStream(audioBytes);
            await blobClient.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = "audio/mpeg"
            });

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "speaking-audio",
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(2)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUrl = blobClient.GenerateSasUri(sasBuilder).ToString();
            _logger.LogInformation("[TTS] Done. URL starts: {Start}", sasUrl[..60]);

            return sasUrl;
        }
    }

    // STT response models
    public class SttResponse
    {
        public string? RecognitionStatus { get; set; }
        public List<SttNBest>? NBest { get; set; }
    }

    public class SttNBest
    {
        public double Confidence { get; set; }
        public string? Lexical { get; set; }
        public string? Display { get; set; }
    }
}
