using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Planner
{
    public interface IPlannerRepository
    {
        Task<StudyPlan> CreateAsync(StudyPlan plan);
        Task<StudyPlan?> GetActiveByUserIdAsync(string userId);
        Task<StudyPlan?> GetByIdAsync(string id, string userId);
        Task<StudyPlan> UpdateAsync(StudyPlan plan);
        Task<List<StudyPlan>> GetAllByUserIdAsync(string userId);
    }
}
