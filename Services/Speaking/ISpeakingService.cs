using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Speaking
{
    public interface ISpeakingService
    {
        Task<SpeakingSession> StartSessionAsync(
            string userId, StartSessionRequest request);
        Task<ConversationTurn> ProcessTurnAsync(
            string sessionId, string userId, ProcessTurnRequest request);
        Task<ConversationTurn> ProcessTextTurnAsync(
            string sessionId, string userId, TextTurnRequest request);
        Task<SpeakingSession> EndSessionAsync(
            string sessionId, string userId, EndSessionRequest request);
        Task<List<SpeakingSession>> GetSessionsAsync(string userId);
        Task<SpeakingSession?> GetSessionAsync(string sessionId, string userId);
        Task<object> GetStatsAsync(string userId);
        Task DeleteSessionAsync(string sessionId, string userId);
    }
}
