namespace BambooBrain_Service.Services.Agents
{
    public interface IStudyAdvisorAgent
    {
        Task<StudyAdvisorResponse> ChatAsync(
            string userId, string message,
            List<AgentMessage>? history = null);
    }

    public class AgentMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class StudyAdvisorResponse
    {
        public string Answer { get; set; } = string.Empty;
        public List<string> ActionsPerformed { get; set; } = new();
        public bool PlanWasAdapted { get; set; }
    }
}
