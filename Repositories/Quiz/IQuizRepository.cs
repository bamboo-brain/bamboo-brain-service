using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Quiz
{
    public interface IQuizRepository
    {
        Task<QuizSession> CreateAsync(QuizSession session);
        Task<QuizSession?> GetByIdAsync(string id, string userId);
        Task<List<QuizSession>> GetByUserIdAsync(string userId, int limit = 20);
        Task<QuizSession> UpdateAsync(QuizSession session);
        Task DeleteAsync(string id, string userId);
    }
}
