using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace BambooBrain_Service.Services.Speaking
{
    public class SpeechService : ISpeechService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly BlobServiceClient _blobClient;
        private readonly ILogger<SpeechService> _logger;

        private string SpeechRegion => _config["AzureSpeech:Region"]!;
        private string SpeechKey => _config["AzureSpeech:ApiKey"]!;

        public SpeechService(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<SpeechService> logger)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("SpeechService");
            _blobClient = new BlobServiceClient(config["BlobStorage:ConnectionString"]);
        }

        // ── STT: Recognize Chinese speech ─────────────────────────────────────

        public async Task<SpeechRecognitionResult> RecognizeAsync(
            string audioBase64, string mimeType)
        {
            try
            {
                var audioBytes = Convert.FromBase64String(audioBase64);

                // Save temp WAV file
                var tempPath = Path.Combine(
                    Path.GetTempPath(), $"{Guid.NewGuid()}.wav");

                await File.WriteAllBytesAsync(tempPath, audioBytes);

                try
                {
                    // Call Azure Speech REST API
                    var url = $"https://{SpeechRegion}.stt.speech.microsoft.com/" +
                              "speech/recognition/conversation/cognitiveservices/v1" +
                              "?language=zh-CN&format=detailed";

                    using var content = new ByteArrayContent(audioBytes);
                    content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");

                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = content
                    };
                    request.Headers.Add("Ocp-Apim-Subscription-Key", SpeechKey);

                    var response = await _httpClient.SendAsync(request);
                    var body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("STT failed: {Body}", body);
                        return new SpeechRecognitionResult
                        {
                            Success = false,
                            Error = $"Speech recognition failed: {response.StatusCode}"
                        };
                    }

                    var result = System.Text.Json.JsonSerializer.Deserialize<SttResponse>(
                        body, new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (result?.RecognitionStatus != "Success" ||
                        result.NBest == null || !result.NBest.Any())
                    {
                        return new SpeechRecognitionResult
                        {
                            Success = false,
                            Error = "No speech recognized"
                        };
                    }

                    var best = result.NBest.First();

                    return new SpeechRecognitionResult
                    {
                        Success = true,
                        Text = best.Display ?? best.Lexical ?? string.Empty,
                        AccuracyScore = best.Confidence * 100
                    };
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "STT exception");
                return new SpeechRecognitionResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        // ── TTS: Synthesize AI response to audio ──────────────────────────────

        public async Task<string> SynthesizeAsync(
            string text, string userId, string sessionId)
        {
            try
            {
                var url = $"https://{SpeechRegion}.tts.speech.microsoft.com/" +
                          "cognitiveservices/v1";

                // Use female Chinese neural voice for Master Ling AI
                var ssml = $"""
                <speak version='1.0' xml:lang='zh-CN'>
                  <voice name='zh-CN-XiaoxiaoNeural'>
                    <prosody rate='0.9'>{System.Security.SecurityElement.Escape(text)}</prosody>
                  </voice>
                </speak>
                """;

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(
                        ssml, System.Text.Encoding.UTF8, "application/ssml+xml")
                };
                request.Headers.Add("Ocp-Apim-Subscription-Key", SpeechKey);
                request.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-128kbitrate-mono-mp3");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("TTS failed: {Status}", response.StatusCode);
                    return string.Empty;
                }

                var audioBytes = await response.Content.ReadAsByteArrayAsync();

                // Upload to blob storage
                var containerClient = _blobClient.GetBlobContainerClient("speaking-audio");
                await containerClient.CreateIfNotExistsAsync();

                var blobName = $"{userId}/{sessionId}/{Guid.NewGuid()}.mp3";
                var blobBlobClient = containerClient.GetBlobClient(blobName);

                using var stream = new MemoryStream(audioBytes);
                await blobBlobClient.UploadAsync(stream, new BlobHttpHeaders
                {
                    ContentType = "audio/mpeg"
                });

                // Return SAS URL (valid 2 hours)
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = "speaking-audio",
                    BlobName = blobName,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(2)
                };
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                return blobBlobClient.GenerateSasUri(sasBuilder).ToString();
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
