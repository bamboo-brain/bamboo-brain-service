using Newtonsoft.Json;

namespace BambooBrain_Service.Models
{
    public class StudyPlan
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = "active"; // "active" | "completed" | "paused"

        [JsonProperty("goal")]
        public StudyGoal Goal { get; set; } = new();

        [JsonProperty("events")]
        public List<StudyEvent> Events { get; set; } = new();

        [JsonProperty("weeklySchedule")]
        public WeeklySchedule WeeklySchedule { get; set; } = new();

        [JsonProperty("agentNotes")]
        public List<AgentNote> AgentNotes { get; set; } = new();

        [JsonProperty("lastAdaptedAt")]
        public DateTime? LastAdaptedAt { get; set; }

        [JsonProperty("nextAdaptationDue")]
        public DateTime NextAdaptationDue { get; set; } =
            DateTime.UtcNow.AddDays(7);

        [JsonProperty("googleCalendarSynced")]
        public bool GoogleCalendarSynced { get; set; } = false;

        [JsonProperty("microsoftCalendarSynced")]
        public bool MicrosoftCalendarSynced { get; set; } = false;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class StudyGoal
    {
        [JsonProperty("targetHskLevel")]
        public int TargetHskLevel { get; set; }

        [JsonProperty("currentHskLevel")]
        public int CurrentHskLevel { get; set; }

        [JsonProperty("targetDate")]
        public DateTime? TargetDate { get; set; }

        [JsonProperty("dailyStudyMinutes")]
        public int DailyStudyMinutes { get; set; } = 30;

        [JsonProperty("focusAreas")]
        public List<string> FocusAreas { get; set; } = new();
        // e.g. ["vocabulary", "speaking", "reading"]

        [JsonProperty("reasonForLearning")]
        public string ReasonForLearning { get; set; } = string.Empty;
    }

    public class StudyEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
        // "flashcards" | "speaking" | "quiz" | "reading" | "review" | "milestone"

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("startTime")]
        public string StartTime { get; set; } = string.Empty; // "09:00"

        [JsonProperty("durationMinutes")]
        public int DurationMinutes { get; set; } = 30;

        [JsonProperty("status")]
        public string Status { get; set; } = "scheduled";
        // "scheduled" | "completed" | "skipped" | "rescheduled"

        [JsonProperty("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [JsonProperty("linkedResourceId")]
        public string? LinkedResourceId { get; set; } // document/deck/session ID

        [JsonProperty("linkedResourceType")]
        public string? LinkedResourceType { get; set; }

        [JsonProperty("googleEventId")]
        public string? GoogleEventId { get; set; }

        [JsonProperty("microsoftEventId")]
        public string? MicrosoftEventId { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; } = "#166534";
    }

    public class WeeklySchedule
    {
        [JsonProperty("preferredDays")]
        public List<string> PreferredDays { get; set; } = new();
        // ["monday","wednesday","friday","saturday"]

        [JsonProperty("preferredTime")]
        public string PreferredTime { get; set; } = "09:00";

        [JsonProperty("sessionsPerWeek")]
        public int SessionsPerWeek { get; set; } = 5;
    }

    public class AgentNote
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
        // "generation" | "adaptation" | "observation"

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}
