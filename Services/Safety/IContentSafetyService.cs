namespace BambooBrain_Service.Services.Safety
{
    public interface IContentSafetyService
    {
        Task<SafetyCheckResult> CheckTextAsync(string text);
        Task<bool> IsSafeAsync(string text);
    }

    public class SafetyCheckResult
    {
        public bool IsSafe { get; set; }
        public string? BlockedReason { get; set; }
        public int HateScore { get; set; }
        public int SelfHarmScore { get; set; }
        public int SexualScore { get; set; }
        public int ViolenceScore { get; set; }
    }
}
