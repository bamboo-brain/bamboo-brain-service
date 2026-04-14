namespace BambooBrain_Service.Services.Search
{
    public interface IRagChatService
    {
        Task<RagChatResponse> ChatAsync(string userId, string question,
            List<RagChatMessage>? history = null);
    }

    public class RagChatMessage
    {
        public string Role { get; set; } = string.Empty; // "user" | "assistant"
        public string Content { get; set; } = string.Empty;
    }

    public class RagChatResponse
    {
        public string Answer { get; set; } = string.Empty;
        public List<RagChunk> Sources { get; set; } = new();
        public bool HasSources { get; set; }
    }
}
