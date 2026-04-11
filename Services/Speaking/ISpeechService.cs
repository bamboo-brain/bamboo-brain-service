namespace BambooBrain_Service.Services.Speaking
{
    public interface ISpeechService
    {
        Task<SpeechRecognitionResult> RecognizeAsync(
            string audioBase64, string mimeType);
        Task<string> SynthesizeAsync(
            string text, string userId, string sessionId);
    }

    public class SpeechRecognitionResult
    {
        public string Text { get; set; } = string.Empty;
        public double AccuracyScore { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
