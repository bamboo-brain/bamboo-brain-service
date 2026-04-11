using BambooBrain_Service.Models;
using BambooBrain_Service.Services.Speaking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/speaking")]
    [Authorize]
    public class SpeakingController : ControllerBase
    {
        private readonly ISpeakingService _speaking;

        public SpeakingController(ISpeakingService speaking)
        {
            _speaking = speaking;
        }

        // Start new session
        [HttpPost("sessions")]
        public async Task<IActionResult> Start([FromBody] StartSessionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Topic))
                return BadRequest(new { message = "Topic is required." });

            try
            {
                var session = await _speaking.StartSessionAsync(userId, request);
                return Ok(session);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Get all sessions
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var sessions = await _speaking.GetSessionsAsync(userId);
            return Ok(sessions);
        }

        // Get single session
        [HttpGet("sessions/{id}")]
        public async Task<IActionResult> GetSession(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var session = await _speaking.GetSessionAsync(id, userId);
            if (session == null) return NotFound();

            return Ok(session);
        }

        // Process audio turn (main speaking endpoint)
        [HttpPost("sessions/{id}/turn/audio")]
        [RequestSizeLimit(10_485_760)] // 10MB for audio
        public async Task<IActionResult> ProcessAudioTurn(
            string id, [FromBody] ProcessTurnRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.AudioBase64))
                return BadRequest(new { message = "Audio data is required." });

            try
            {
                var turn = await _speaking.ProcessTurnAsync(id, userId, request);
                return Ok(turn);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Process text turn (fallback / testing)
        [HttpPost("sessions/{id}/turn/text")]
        public async Task<IActionResult> ProcessTextTurn(
            string id, [FromBody] TextTurnRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { message = "Text is required." });

            try
            {
                var turn = await _speaking.ProcessTextTurnAsync(id, userId, request);
                return Ok(turn);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // End session
        [HttpPost("sessions/{id}/end")]
        public async Task<IActionResult> End(
            string id, [FromBody] EndSessionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var session = await _speaking.EndSessionAsync(id, userId, request);
                return Ok(session);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Delete session
        [HttpDelete("sessions/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            await _speaking.DeleteSessionAsync(id, userId);
            return NoContent();
        }

        // Get stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var stats = await _speaking.GetStatsAsync(userId);
            return Ok(stats);
        }

        // Get suggested topics
        [HttpGet("topics")]
        public IActionResult GetTopics()
        {
            var topics = new[]
            {
            new { id = "1", topic = "Ordering Coffee in Beijing",
                  description = "Practice ordering drinks at a café",
                  hskLevel = 2, emoji = "☕" },
            new { id = "2", topic = "Job Interview in Shanghai",
                  description = "Practice professional Chinese for interviews",
                  hskLevel = 5, emoji = "💼" },
            new { id = "3", topic = "Shopping at the Market",
                  description = "Bargaining and buying at a local market",
                  hskLevel = 2, emoji = "🛒" },
            new { id = "4", topic = "Discussing Contemporary Art",
                  description = "Talk about Chinese art and culture",
                  hskLevel = 6, emoji = "🎨" },
            new { id = "5", topic = "Planning a Trip to Chengdu",
                  description = "Travel planning, directions, and attractions",
                  hskLevel = 3, emoji = "🐼" },
            new { id = "6", topic = "Talking About Your Family",
                  description = "Describe family members and relationships",
                  hskLevel = 1, emoji = "👨‍👩‍👧" },
            new { id = "7", topic = "Visiting a Doctor",
                  description = "Describe symptoms and medical situations",
                  hskLevel = 4, emoji = "🏥" },
            new { id = "8", topic = "Discussing Chinese Cuisine",
                  description = "Talk about food preferences and recipes",
                  hskLevel = 3, emoji = "🍜" },
        };

            return Ok(topics);
        }
    }

}
