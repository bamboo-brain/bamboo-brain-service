namespace BambooBrain_Service.Models
{
    public class UpsertOAuthUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty; // "google" | "azure-ad"
    }
}
