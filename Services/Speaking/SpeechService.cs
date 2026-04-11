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

        public async Task<SpeechRecognitionResult> RecognizeAsync(
            string audioBase64, string mimeType)
        {
            try
            {
                var audioBytes = Convert.FromBase64String(audioBase64);

                var url = $"https://{SpeechRegion}.stt.speech.microsoft.com/" +
                          "speech/recognition/conversation/cognitiveservices/v1" +
                          "?language=zh-CN&format=detailed";

                using var httpClient = new HttpClient();
                using var content = new ByteArrayContent(audioBytes);
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };
                request.Headers.Add("Ocp-Apim-Subscription-Key", SpeechKey);

                var response = await httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("STT response: {Status} {Body}",
                    response.StatusCode, body[..Math.Min(200, body.Length)]);

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
                    result.NBest == null || !result.NBest.Any())
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

        public async Task<string> SynthesizeAsync(
            string text, string userId, string sessionId)
        {
            try
            {
                _logger.LogInformation("TTS starting for text: {Text}", text);

                var url = $"https://{SpeechRegion}.tts.speech.microsoft.com/" +
                          "cognitiveservices/v1";

                var ssml = $"""
                <speak version='1.0' xml:lang='zh-CN'>
                  <voice name='zh-CN-XiaoxiaoNeural'>
                    <prosody rate='0.9'>{System.Security.SecurityElement.Escape(text)}</prosody>
                  </voice>
                </speak>
                """;

                using var httpClient = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(
                        ssml, System.Text.Encoding.UTF8, "application/ssml+xml")
                };
                request.Headers.Add("Ocp-Apim-Subscription-Key", SpeechKey);
                request.Headers.Add("X-Microsoft-OutputFormat",
                    "audio-16khz-128kbitrate-mono-mp3");

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("TTS failed: {Status} {Body}",
                        response.StatusCode, errorBody);
                    return string.Empty;
                }

                var audioBytes = await response.Content.ReadAsByteArrayAsync();
                _logger.LogInformation("TTS audio bytes: {Length}", audioBytes.Length);

                // ← Same pattern as AudioExtractionService.GenerateSasUrlAsync
                var blobServiceClient = new BlobServiceClient(BlobConnectionString);
                var containerClient = blobServiceClient
                    .GetBlobContainerClient("speaking-audio");

                // Create container if it doesn't exist
                await containerClient.CreateIfNotExistsAsync();

                var blobName = $"{userId}/{sessionId}/{Guid.NewGuid()}.mp3";
                var blobClient = containerClient.GetBlobClient(blobName);

                using var stream = new MemoryStream(audioBytes);
                await blobClient.UploadAsync(stream, new BlobHttpHeaders
                {
                    ContentType = "audio/mpeg"
                });

                _logger.LogInformation("TTS uploaded to blob: {BlobName}", blobName);

                // ← Same SAS pattern as AudioExtractionService
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = "speaking-audio",
                    BlobName = blobName,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(2)
                };
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                var sasUrl = blobClient.GenerateSasUri(sasBuilder).ToString();
                _logger.LogInformation("TTS SAS URL generated successfully");

                return sasUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TTS exception");
                return string.Empty;
            }
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
