using Newtonsoft.Json;

namespace BambooBrain_Service.Models
{
    public class QuizSession
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("sourceType")]
        public string SourceType { get; set; } = string.Empty; // "document" | "deck"

        [JsonProperty("sourceId")]
        public string SourceId { get; set; } = string.Empty;

        [JsonProperty("sourceName")]
        public string SourceName { get; set; } = string.Empty;

        [JsonProperty("questions")]
        public List<QuizQuestion> Questions { get; set; } = new();

        [JsonProperty("answers")]
        public List<QuizAnswer> Answers { get; set; } = new();

        [JsonProperty("status")]
        public string Status { get; set; } = "in-progress"; // "in-progress" | "completed"

        [JsonProperty("score")]
        public double? Score { get; set; } // 0-100 percentage

        [JsonProperty("totalQuestions")]
        public int TotalQuestions => Questions.Count;

        [JsonProperty("correctAnswers")]
        public int CorrectAnswers => Answers.Count(a => a.IsCorrect);

        [JsonProperty("timeStarted")]
        public DateTime TimeStarted { get; set; } = DateTime.UtcNow;

        [JsonProperty("timeCompleted")]
        public DateTime? TimeCompleted { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class QuizQuestion
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
        // "multiple-choice" | "fill-in-blank" | "tone-identification" | "listening"

        [JsonProperty("question")]
        public string Question { get; set; } = string.Empty;

        [JsonProperty("word")]
        public string Word { get; set; } = string.Empty;      // target Chinese word

        [JsonProperty("pinyin")]
        public string? Pinyin { get; set; }

        [JsonProperty("pinyinWithoutTones")]
        public string? PinyinWithoutTones { get; set; }       // for tone identification

        [JsonProperty("options")]
        public List<string> Options { get; set; } = new();    // for multiple choice

        [JsonProperty("correctAnswer")]
        public string CorrectAnswer { get; set; } = string.Empty;

        [JsonProperty("explanation")]
        public string? Explanation { get; set; }

        [JsonProperty("hskLevel")]
        public int? HskLevel { get; set; }

        [JsonProperty("audioText")]
        public string? AudioText { get; set; }                // text to speak for listening
    }

    public class QuizAnswer
    {
        [JsonProperty("questionId")]
        public string QuestionId { get; set; } = string.Empty;

        [JsonProperty("userAnswer")]
        public string UserAnswer { get; set; } = string.Empty;

        [JsonProperty("isCorrect")]
        public bool IsCorrect { get; set; }

        [JsonProperty("timeSpentSeconds")]
        public int TimeSpentSeconds { get; set; }

        [JsonProperty("answeredAt")]
        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
    }

}
