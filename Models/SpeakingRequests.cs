namespace BambooBrain_Service.Models
{
    public class StartSessionRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string TopicDescription { get; set; } = string.Empty;
        public int HskLevel { get; set; } = 1;
    }

    public class ProcessTurnRequest
    {
        public string AudioBase64 { get; set; } = string.Empty; // base64 WAV audio
        public string MimeType { get; set; } = "audio/wav";
    }

    public class TextTurnRequest // fallback if no audio
    {
        public string Text { get; set; } = string.Empty;
    }

    public class EndSessionRequest
    {
        public int DurationSeconds { get; set; }
    }
}
