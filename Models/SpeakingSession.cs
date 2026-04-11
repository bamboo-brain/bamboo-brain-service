using Newtonsoft.Json;

namespace BambooBrain_Service.Models
{
    public class SpeakingSession
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("topic")]
        public string Topic { get; set; } = string.Empty;

        [JsonProperty("topicDescription")]
        public string TopicDescription { get; set; } = string.Empty;

        [JsonProperty("hskLevel")]
        public int HskLevel { get; set; } = 1;

        [JsonProperty("status")]
        public string Status { get; set; } = "active"; // "active" | "completed"

        [JsonProperty("turns")]
        public List<ConversationTurn> Turns { get; set; } = new();

        [JsonProperty("insights")]
        public SessionInsights? Insights { get; set; }

        [JsonProperty("topVocabulary")]
        public List<VocabularyItem> TopVocabulary { get; set; } = new();

        [JsonProperty("startedAt")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("endedAt")]
        public DateTime? EndedAt { get; set; }

        [JsonProperty("durationSeconds")]
        public int DurationSeconds { get; set; } = 0;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ConversationTurn
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("role")]
        public string Role { get; set; } = string.Empty; // "ai" | "user"

        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;   // Chinese text

        [JsonProperty("pinyin")]
        public string? Pinyin { get; set; }

        [JsonProperty("translation")]
        public string? Translation { get; set; }           // English translation

        [JsonProperty("toneCorrections")]
        public List<ToneCorrection> ToneCorrections { get; set; } = new();

        [JsonProperty("accuracyScore")]
        public double? AccuracyScore { get; set; }         // 0-100

        [JsonProperty("audioUrl")]
        public string? AudioUrl { get; set; }              // SAS URL for AI TTS audio

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ToneCorrection
    {
        [JsonProperty("word")]
        public string Word { get; set; } = string.Empty;

        [JsonProperty("correctPinyin")]
        public string CorrectPinyin { get; set; } = string.Empty;

        [JsonProperty("spokenPinyin")]
        public string SpokenPinyin { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
        // e.g. "měishì (3rd tone) was pronounced as 2nd tone"
    }

    public class SessionInsights
    {
        [JsonProperty("accuracyScore")]
        public double AccuracyScore { get; set; }  // 0-100

        [JsonProperty("fluency")]
        public string Fluency { get; set; } = string.Empty;
        // "Excellent" | "Great" | "Good" | "Needs Work"

        [JsonProperty("totalTurns")]
        public int TotalTurns { get; set; }

        [JsonProperty("userTurns")]
        public int UserTurns { get; set; }

        [JsonProperty("toneErrorCount")]
        public int ToneErrorCount { get; set; }

        [JsonProperty("vocabularyCount")]
        public int VocabularyCount { get; set; }

        [JsonProperty("duration")]
        public string Duration { get; set; } = string.Empty; // "12:40"
    }

    public class VocabularyItem
    {
        [JsonProperty("word")]
        public string Word { get; set; } = string.Empty;

        [JsonProperty("pinyin")]
        public string Pinyin { get; set; } = string.Empty;

        [JsonProperty("meaning")]
        public string Meaning { get; set; } = string.Empty;

        [JsonProperty("usageCount")]
        public int UsageCount { get; set; } = 1;
    }
}