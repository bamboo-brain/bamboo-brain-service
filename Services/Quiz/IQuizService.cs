using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Quiz
{
    public interface IQuizService
    {
        Task<QuizSession> GenerateQuizAsync(string userId, GenerateQuizRequest request);
        Task<QuizSession> SubmitAnswerAsync(string sessionId, string userId, SubmitAnswerRequest request);
        Task<QuizSession> CompleteQuizAsync(string sessionId, string userId);
        Task<List<QuizSession>> GetSessionsAsync(string userId);
        Task<QuizSession?> GetSessionAsync(string sessionId, string userId);
        Task DeleteSessionAsync(string sessionId, string userId);
        Task<object> GetStatsAsync(string userId);
    }
}
