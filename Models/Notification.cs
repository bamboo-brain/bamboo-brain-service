using Newtonsoft.Json;

namespace BambooBrain_Service.Models
{
    public class Notification
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
        // "achievement" | "processing_complete" | "tip" | "system"
        // "streak_reminder" | "adaptation" | "plan_due"

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("isRead")]
        public bool IsRead { get; set; } = false;

        [JsonProperty("resourceId")]
        public string? ResourceId { get; set; }    // linked document/deck/session id

        [JsonProperty("resourceType")]
        public string? ResourceType { get; set; }  // "document" | "deck" | "session"

        [JsonProperty("actionUrl")]
        public string? ActionUrl { get; set; }     // e.g. "/library/doc-id"

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
