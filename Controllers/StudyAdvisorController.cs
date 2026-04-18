using BambooBrain_Service.Services.Agents;
using BambooBrain_Service.Services.Safety;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/advisor")]
    [Authorize]
    public class StudyAdvisorController : ControllerBase
    {
        private readonly IStudyAdvisorAgent _advisor;
        private readonly IContentSafetyService _safety;
        private readonly ILogger<StudyAdvisorController> _logger;

        public StudyAdvisorController(IStudyAdvisorAgent advisor, ILogger<StudyAdvisorController> logger, IContentSafetyService safety)
        {
            _advisor = advisor;
            _logger = logger;
            _safety = safety;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] AdvisorChatRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { message = "Message is required." });

            try
            {
                var safetyCheck = await _safety.CheckTextAsync(request.Message);
                if (!safetyCheck.IsSafe)
                {
                    _logger.LogWarning(
                        "[Safety] Blocked message from user {UserId}: {Reason}",
                        userId, safetyCheck.BlockedReason);
                    return BadRequest(new
                    {
                        message = "Your message was flagged by our content safety system. " +
                                  "Please keep conversations focused on Chinese language learning.",
                        blocked = true,
                        reason = safetyCheck.BlockedReason
                    });
                }

                var response = await _advisor.ChatAsync(
                    userId, request.Message, request.History);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }

    public class AdvisorChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<AgentMessage>? History { get; set; }
    }
}
