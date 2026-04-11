namespace BambooBrain_Service.Models
{
    public class CreatePlanRequest
    {
        public StudyGoal Goal { get; set; } = new();
        public WeeklySchedule WeeklySchedule { get; set; } = new();
        public bool SyncToGoogleCalendar { get; set; } = false;
        public bool SyncToMicrosoftCalendar { get; set; } = false;
        public string? GoogleAccessToken { get; set; }
        public string? MicrosoftAccessToken { get; set; }
    }

    public class UpdateEventRequest
    {
        public string Status { get; set; } = string.Empty;
        public DateTime? CompletedAt { get; set; }
        public string? LinkedResourceId { get; set; }
        public string? LinkedResourceType { get; set; }
    }

    public class RescheduleEventRequest
    {
        public DateTime NewDate { get; set; }
        public string NewStartTime { get; set; } = string.Empty;
    }

    public class RecordActivityRequest
    {
        public string ActivityType { get; set; } = string.Empty;
        // "flashcard_review" | "quiz" | "speaking" | "document_upload"
        public int MinutesSpent { get; set; }
        public int ItemsCompleted { get; set; }
        public string? ResourceId { get; set; }
    }
}
