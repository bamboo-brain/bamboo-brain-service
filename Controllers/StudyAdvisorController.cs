using BambooBrain_Service.Services.Agents;
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

        public StudyAdvisorController(IStudyAdvisorAgent advisor)
        {
            _advisor = advisor;
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
