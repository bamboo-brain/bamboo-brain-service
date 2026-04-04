namespace BambooBrain_Service.Models
{
    public class TranscriptResult
    {
        public string Transcript { get; set; } = string.Empty;
        public string? Duration { get; set; }
        public List<WordTiming> WordTimings { get; set; } = new();
    }

    public class TranscriptionResponse
    {
        public string? Self { get; set; }
        public string? Status { get; set; }
    }

    public class TranscriptionStatusResponse
    {
        public string? Status { get; set; }
        public TranscriptionProperties? Properties { get; set; }
    }

    public class TranscriptionProperties
    {
        public string? Error { get; set; }
    }

    public class TranscriptionFilesResponse
    {
        public List<TranscriptionFile>? Values { get; set; }
    }

    public class TranscriptionFile
    {
        public string? Kind { get; set; }
        public TranscriptionFileLinks? Links { get; set; }
    }

    public class TranscriptionFileLinks
    {
        public string? ContentUrl { get; set; }
    }

    public class TranscriptContent
    {
        public List<CombinedPhrase>? CombinedRecognizedPhrases { get; set; }
        public List<RecognizedPhrase>? RecognizedPhrases { get; set; }
    }

    public class CombinedPhrase
    {
        public string? Display { get; set; }
    }

    public class RecognizedPhrase
    {
        public string? Offset { get; set; }
        public string? Duration { get; set; }
        public List<NBest>? NBest { get; set; }
    }

    public class NBest
    {
        public string? Display { get; set; }
        public List<WordTiming>? Words { get; set; }
    }

    public class WordTiming
    {
        public string? Word { get; set; }
        public string? Offset { get; set; }   // ISO 8601 duration
        public string? Duration { get; set; } // ISO 8601 duration
    }
}
