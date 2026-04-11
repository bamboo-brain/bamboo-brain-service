using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Stats
{
    public interface IStatsRepository
    {
        Task<UserStats> GetOrCreateAsync(string userId);
        Task<UserStats> UpdateAsync(UserStats stats);
    }
}
