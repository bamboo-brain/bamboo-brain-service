namespace BambooBrain_Service.Models
{
    public class UpdateProfileRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
    }

    public class UpdateEmailRequest
    {
        public string NewEmail { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class UpdatePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UpdateIntegrationRequest
    {
        public bool IsGcalSyncEnabled { get; set; }
        public bool IsMicrosoftAccountEnabled { get; set; }
    }
}
