using Newtonsoft.Json;

namespace BambooBrain_Service.Models
{
    public class FlashcardDeck
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonProperty("sourceDocumentId")]
        public string? SourceDocumentId { get; set; } // null if manually created

        [JsonProperty("sourceType")]
        public string SourceType { get; set; } = "manual"; // "manual" | "document" | "custom"

        [JsonProperty("cards")]
        public List<Flashcard> Cards { get; set; } = new();

        [JsonProperty("totalCards")]
        public int TotalCards => Cards.Count;

        [JsonProperty("dueToday")]
        public int DueToday => Cards.Count(c => c.NextReviewDate <= DateTime.UtcNow);

        [JsonProperty("masteryPercentage")]
        public double MasteryPercentage => Cards.Any()
            ? Math.Round(Cards.Average(c => Math.Min(c.EaseFactor / 3.0 * 100, 100)), 1)
            : 0;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Flashcard
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("word")]
        public string Word { get; set; } = string.Empty;       // Chinese characters

        [JsonProperty("pinyin")]
        public string Pinyin { get; set; } = string.Empty;

        [JsonProperty("meaning")]
        public string Meaning { get; set; } = string.Empty;    // English meaning

        [JsonProperty("exampleSentence")]
        public string? ExampleSentence { get; set; }           // Chinese example

        [JsonProperty("exampleTranslation")]
        public string? ExampleTranslation { get; set; }        // English translation

        [JsonProperty("hskLevel")]
        public int? HskLevel { get; set; }

        [JsonProperty("imageUrl")]
        public string? ImageUrl { get; set; }

        // ── SM-2 Algorithm fields ──────────────────────────────────────────────

        [JsonProperty("repetitions")]
        public int Repetitions { get; set; } = 0;              // times reviewed

        [JsonProperty("easeFactor")]
        public double EaseFactor { get; set; } = 2.5;          // difficulty multiplier (min 1.3)

        [JsonProperty("intervalDays")]
        public int IntervalDays { get; set; } = 1;             // days until next review

        [JsonProperty("nextReviewDate")]
        public DateTime NextReviewDate { get; set; } = DateTime.UtcNow; // due date

        [JsonProperty("lastReviewDate")]
        public DateTime? LastReviewDate { get; set; }

        [JsonProperty("lastGrade")]
        public int LastGrade { get; set; } = 0;                // 0-5 last rating

        [JsonProperty("totalReviews")]
        public int TotalReviews { get; set; } = 0;

        [JsonProperty("correctStreak")]
        public int CorrectStreak { get; set; } = 0;
    }

}
