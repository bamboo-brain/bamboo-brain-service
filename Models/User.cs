using Newtonsoft.Json;

namespace BambooBrain_Service.Models
{
    public class User
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("image")]
        public string Image { get; set; } = string.Empty;

        [JsonProperty("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string Provider { get; set; } = "credentials"; // "google" | "azure-ad" | "credentials"

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("hskLevel")]
        public int HskLevel { get; set; } = 0;

        [JsonProperty("isGcalSyncEnabled")]
        public bool IsGcalSyncEnabled { get; set; } = false;

        [JsonProperty("isMicrosoftAccountEnabled")]
        public bool IsMicrosoftAccountEnabled { get; set; } = false;

        [JsonProperty("areaOfInterests")]
        public List<string> AreaOfInterests { get; set; } = new List<string>();

        [JsonProperty("isOnboardingComplete")]
        public bool IsOnboardingComplete { get; set; } = false;
    }
}
