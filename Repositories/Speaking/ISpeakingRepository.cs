using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Speaking
{
    public interface ISpeakingRepository
    {
        Task<SpeakingSession> CreateAsync(SpeakingSession session);
        Task<SpeakingSession?> GetByIdAsync(string id, string userId);
        Task<List<SpeakingSession>> GetByUserIdAsync(string userId, int limit = 20);
        Task<SpeakingSession> UpdateAsync(SpeakingSession session);
        Task DeleteAsync(string id, string userId);
    }
}
