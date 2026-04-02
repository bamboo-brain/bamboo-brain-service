namespace BambooBrain_Service.Models
{
    public class OnboardingRequest
    {
        public int HskLevel { get; set; } = 1;
        public bool IsGcalSyncEnabled { get; set; } = false;
        public bool IsMicrosoftAccountEnabled { get; set; } = false;
        public List<string> AreaOfInterests { get; set; } = new();
    }
}
