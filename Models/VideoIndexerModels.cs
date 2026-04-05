using System.Text.Json.Serialization;

namespace BambooBrain_Service.Models
{
    public class VideoIndexerUploadResponse
    {
        public string? Id { get; set; }
        public string? State { get; set; }
    }

    public class VideoIndexerStatusResponse
    {
        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("videos")]
        public List<VideoData>? Videos { get; set; } // Note: This is a List/Array
    }

    public class VideoData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("insights")]
        public VideoInsights? Insights { get; set; }
    }

    public class VideoInsights
    {
        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("transcript")]
        public List<TranscriptSegment>? Transcript { get; set; }
    }

    public class TranscriptSegment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("instances")]
        public List<Instance>? Instances { get; set; }
    }

    public class Instance
    {
        [JsonPropertyName("start")]
        public string? Start { get; set; }

        [JsonPropertyName("end")]
        public string? End { get; set; }
    }

    public class VideoIndexerVideos
    {
        public List<VideoIndexerVideo>? List { get; set; }
    }

    public class VideoIndexerVideo
    {
        public VideoIndexerInsights? Insights { get; set; }
    }

    public class VideoIndexerInsights
    {
        public string? Duration { get; set; }
        public List<VideoIndexerTranscript>? Transcript { get; set; }
        public List<VideoIndexerKeyword>? Keywords { get; set; }
    }

    public class VideoIndexerTranscript
    {
        public string? Text { get; set; }
        public List<VideoIndexerInstance>? Instances { get; set; }
    }

    public class VideoIndexerKeyword
    {
        public string? Text { get; set; }
        public List<VideoIndexerInstance>? Instances { get; set; }
    }

    public class VideoIndexerInstance
    {
        public string? Start { get; set; }   // e.g. "0:00:01.5"
        public string? End { get; set; }
    }

    public class VideoTranscriptResult
    {
        public string Transcript { get; set; } = string.Empty;
        public string? Duration { get; set; }
        public List<WordTiming> WordTimings { get; set; } = new();
    }
}
