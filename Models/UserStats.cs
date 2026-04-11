using Newtonsoft.Json;

namespace BambooBrain_Service.Models
{
    public class UserStats
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        // ── Streaks ────────────────────────────────────────────────────────────
        [JsonProperty("currentStreak")]
        public int CurrentStreak { get; set; } = 0;

        [JsonProperty("longestStreak")]
        public int LongestStreak { get; set; } = 0;

        [JsonProperty("lastActivityDate")]
        public DateTime? LastActivityDate { get; set; }

        [JsonProperty("streakHistory")]
        public List<StreakDay> StreakHistory { get; set; } = new();

        // ── Vocabulary ─────────────────────────────────────────────────────────
        [JsonProperty("totalCharactersLearned")]
        public int TotalCharactersLearned { get; set; } = 0;

        [JsonProperty("charactersThisWeek")]
        public int CharactersThisWeek { get; set; } = 0;

        [JsonProperty("hskProgress")]
        public List<HskLevelProgress> HskProgress { get; set; } = new();

        // ── Study time ─────────────────────────────────────────────────────────
        [JsonProperty("totalStudyMinutes")]
        public int TotalStudyMinutes { get; set; } = 0;

        [JsonProperty("studyMinutesThisWeek")]
        public int StudyMinutesThisWeek { get; set; } = 0;

        [JsonProperty("studyMinutesToday")]
        public int StudyMinutesToday { get; set; } = 0;

        // ── Activity breakdown ─────────────────────────────────────────────────
        [JsonProperty("totalFlashcardsReviewed")]
        public int TotalFlashcardsReviewed { get; set; } = 0;

        [JsonProperty("totalQuizzesCompleted")]
        public int TotalQuizzesCompleted { get; set; } = 0;

        [JsonProperty("totalSpeakingSessions")]
        public int TotalSpeakingSessions { get; set; } = 0;

        [JsonProperty("totalDocumentsUploaded")]
        public int TotalDocumentsUploaded { get; set; } = 0;

        // ── Retention curve ────────────────────────────────────────────────────
        [JsonProperty("retentionCurve")]
        public List<RetentionDataPoint> RetentionCurve { get; set; } = new();

        // ── Daily activity log ─────────────────────────────────────────────────
        [JsonProperty("dailyActivity")]
        public List<DailyActivity> DailyActivity { get; set; } = new();

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class StreakDay
    {
        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("minutesStudied")]
        public int MinutesStudied { get; set; }
    }

    public class HskLevelProgress
    {
        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("wordsLearned")]
        public int WordsLearned { get; set; }

        [JsonProperty("totalWords")]
        public int TotalWords { get; set; }
        // HSK1=150, HSK2=150, HSK3=300, HSK4=600, HSK5=1300, HSK6=2500

        [JsonProperty("percentage")]
        public double Percentage => TotalWords > 0
            ? Math.Round((double)WordsLearned / TotalWords * 100, 1)
            : 0;
    }

    public class RetentionDataPoint
    {
        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("retentionRate")]
        public double RetentionRate { get; set; } // 0-100
    }

    public class DailyActivity
    {
        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("minutesStudied")]
        public int MinutesStudied { get; set; }

        [JsonProperty("flashcardsReviewed")]
        public int FlashcardsReviewed { get; set; }

        [JsonProperty("quizzesCompleted")]
        public int QuizzesCompleted { get; set; }

        [JsonProperty("speakingMinutes")]
        public int SpeakingMinutes { get; set; }

        [JsonProperty("wordsLearned")]
        public int WordsLearned { get; set; }
    }

}
