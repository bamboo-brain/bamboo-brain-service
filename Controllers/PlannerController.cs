using BambooBrain_Service.Models;
using BambooBrain_Service.Services.Planner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/planner")]
    [Authorize]
    public class PlannerController : ControllerBase
    {
        private readonly IPlannerService _planner;

        public PlannerController(IPlannerService planner)
        {
            _planner = planner;
        }

        // Create plan (runs GoalAgent)
        [HttpPost("plans")]
        public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var plan = await _planner.CreatePlanAsync(userId, request);
                return Ok(plan);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Get active plan
        [HttpGet("plans/active")]
        public async Task<IActionResult> GetActivePlan()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var plan = await _planner.GetActivePlanAsync(userId);
            if (plan == null)
                return Ok(new { hasPlan = false });

            return Ok(plan);
        }

        // Get calendar events for a month
        [HttpGet("calendar")]
        public async Task<IActionResult> GetCalendar(
            [FromQuery] int year, [FromQuery] int month)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (year == 0) year = DateTime.UtcNow.Year;
            if (month == 0) month = DateTime.UtcNow.Month;

            var events = await _planner.GetCalendarEventsAsync(userId, year, month);
            return Ok(events);
        }

        // Mark event complete/skipped
        [HttpPatch("plans/{planId}/events/{eventId}")]
        public async Task<IActionResult> UpdateEvent(
            string planId, string eventId,
            [FromBody] UpdateEventRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var plan = await _planner.UpdateEventStatusAsync(
                    planId, eventId, userId, request);
                return Ok(plan);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Reschedule event
        [HttpPatch("plans/{planId}/events/{eventId}/reschedule")]
        public async Task<IActionResult> RescheduleEvent(
            string planId, string eventId,
            [FromBody] RescheduleEventRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var plan = await _planner.RescheduleEventAsync(
                    planId, eventId, userId, request);
                return Ok(plan);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Run weekly adaptation (MonitorAgent + AdaptAgent)
        [HttpPost("plans/{planId}/adapt")]
        public async Task<IActionResult> AdaptPlan(string planId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var plan = await _planner.RunWeeklyAdaptationAsync(planId, userId);
                return Ok(plan);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Record study activity (updates stats + streak)
        [HttpPost("activity")]
        public async Task<IActionResult> RecordActivity(
            [FromBody] RecordActivityRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            await _planner.RecordActivityAsync(userId, request);
            return Ok(new { message = "Activity recorded." });
        }

        // Get user stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var stats = await _planner.GetStatsAsync(userId);
            return Ok(stats);
        }
    }

}
