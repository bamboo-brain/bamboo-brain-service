using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Planner
{
    public interface IPlannerService
    {
        Task<StudyPlan> CreatePlanAsync(string userId, CreatePlanRequest request);
        Task<StudyPlan?> GetActivePlanAsync(string userId);
        Task<StudyPlan> UpdateEventStatusAsync(string planId, string eventId,
            string userId, UpdateEventRequest request);
        Task<StudyPlan> RescheduleEventAsync(string planId, string eventId,
            string userId, RescheduleEventRequest request);
        Task<StudyPlan> RunWeeklyAdaptationAsync(string planId, string userId);
        Task RecordActivityAsync(string userId, RecordActivityRequest request);
        Task<UserStats> GetStatsAsync(string userId);
        Task<object> GetCalendarEventsAsync(string userId, int year, int month);
    }
}
