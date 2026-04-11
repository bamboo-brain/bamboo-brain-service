using BambooBrain_Service.Models;
using BambooBrain_Service.Services.Quiz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/quiz")]
    [Authorize]
    public class QuizController : ControllerBase
    {
        private readonly IQuizService _quiz;

        public QuizController(IQuizService quiz)
        {
            _quiz = quiz;
        }

        // Generate new quiz session
        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] GenerateQuizRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var session = await _quiz.GenerateQuizAsync(userId, request);
                return Ok(session);
            }
            catch (InvalidOperationException ex)
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

            var sessions = await _quiz.GetSessionsAsync(userId);
            return Ok(sessions);
        }

        // Get single session
        [HttpGet("sessions/{id}")]
        public async Task<IActionResult> GetSession(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var session = await _quiz.GetSessionAsync(id, userId);
            if (session == null) return NotFound();

            return Ok(session);
        }

        // Submit answer
        [HttpPost("sessions/{id}/answer")]
        public async Task<IActionResult> SubmitAnswer(
            string id, [FromBody] SubmitAnswerRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.QuestionId))
                return BadRequest(new { message = "QuestionId is required." });

            if (string.IsNullOrWhiteSpace(request.UserAnswer))
                return BadRequest(new { message = "UserAnswer is required." });

            try
            {
                var session = await _quiz.SubmitAnswerAsync(id, userId, request);

                // Return the updated answer with correctness
                var answer = session.Answers.Last();
                var question = session.Questions.First(q => q.Id == request.QuestionId);

                return Ok(new
                {
                    isCorrect = answer.IsCorrect,
                    correctAnswer = question.CorrectAnswer,
                    explanation = question.Explanation,
                    session = new
                    {
                        session.Id,
                        session.Status,
                        session.CorrectAnswers,
                        session.TotalQuestions,
                        answeredCount = session.Answers.Count
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Complete quiz
        [HttpPost("sessions/{id}/complete")]
        public async Task<IActionResult> Complete(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var session = await _quiz.CompleteQuizAsync(id, userId);
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

            await _quiz.DeleteSessionAsync(id, userId);
            return NoContent();
        }

        // Overall stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var stats = await _quiz.GetStatsAsync(userId);
            return Ok(stats);
        }

        // Stats per document
        [HttpGet("stats/document/{documentId}")]
        public async Task<IActionResult> GetDocumentStats(string documentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var sessions = await _quiz.GetSessionsAsync(userId);
            var docSessions = sessions
                .Where(s => s.SourceId == documentId &&
                            s.Status == "completed")
                .ToList();

            return Ok(new
            {
                documentId,
                totalQuizzes = docSessions.Count,
                averageScore = docSessions.Any()
                    ? Math.Round(docSessions.Average(s => s.Score ?? 0), 1)
                    : 0,
                bestScore = docSessions.Any()
                    ? docSessions.Max(s => s.Score ?? 0)
                    : 0,
                recentSessions = docSessions.Take(5).Select(s => new
                {
                    s.Id,
                    s.Score,
                    s.TotalQuestions,
                    s.CorrectAnswers,
                    s.CreatedAt
                })
            });
        }
    }
}
