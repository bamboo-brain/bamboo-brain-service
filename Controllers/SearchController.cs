using BambooBrain_Service.Repositories.Documents;
using BambooBrain_Service.Services.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/search")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly IAISearchService _search;
        private readonly IRagChatService _ragChat;
        private readonly IDocumentRepository _documentRepository;
        private readonly ILogger<SearchController> _logger;

        public SearchController(IAISearchService search, IRagChatService ragChat, IDocumentRepository documentRepository, ILogger<SearchController> logger)
        {
            _search = search;
            _ragChat = ragChat;
            _documentRepository = documentRepository;
            _logger = logger;
        }

        // Semantic library search
        [HttpGet("documents")]
        public async Task<IActionResult> SearchDocuments(
            [FromQuery] string q,
            [FromQuery] int top = 10,
            [FromQuery] string? fileType = null,
            [FromQuery] int? hskLevel = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { message = "Query is required." });

            var result = await _search.SearchDocumentsAsync(
                userId, q, top, fileType, hskLevel);
            return Ok(result);
        }

        // RAG chatbot
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { message = "Question is required." });

            var response = await _ragChat.ChatAsync(
                userId, request.Question, request.History);
            return Ok(response);
        }

        // Manually trigger indexing (useful for demo)
        [HttpPost("index/{documentId}")]
        public async Task<IActionResult> IndexDocument(string documentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            // Fetch the document and re-index it
            var document = await _documentRepository.GetByIdAsync(documentId, userId);
            if (document == null) return NotFound();

            if (document.ExtractionStatus != "ready")
                return BadRequest(new { message = "Document extraction not complete yet." });

            _ = Task.Run(async () =>
            {
                await _search.IndexDocumentAsync(document);
                _logger.LogInformation("Re-indexed document {Id}", documentId);
            });

            return Ok(new { message = "Re-indexing started." });
        }
    }

    public class ChatRequest
    {
        public string Question { get; set; } = string.Empty;
        public List<RagChatMessage>? History { get; set; }
    }

}
