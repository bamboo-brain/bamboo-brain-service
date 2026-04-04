using BambooBrain_Service.Services.BlobStorage;
using BambooBrain_Service.Services.Document;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/documents")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IBlobStorageService _blobStorage;

        public DocumentsController(IDocumentService documentService, IBlobStorageService blobStorage)
        {
            _documentService = documentService;
            _blobStorage = blobStorage;
        }

        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided." });

            if (file.Length > 104_857_600)
                return BadRequest(new { message = "File size exceeds 100MB limit." });

            try
            {
                var document = await _documentService.UploadDocumentAsync(userId, file);
                return Ok(document);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageSize = 10,
            [FromQuery] string? continuationToken = null,
            [FromQuery] string? fileType = null,      // "pdf" | "video" | "audio" | "ppt" | "all"
            [FromQuery] int? hskLevel = null,          // 1-6
            [FromQuery] string? search = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (pageSize < 1 || pageSize > 50)
                return BadRequest(new { message = "pageSize must be between 1 and 50." });

            var (items, nextToken, totalCount) = await _documentService.GetUserDocumentsAsync(
                userId, pageSize, continuationToken, fileType, hskLevel, search);

            return Ok(new
            {
                items,
                pagination = new
                {
                    pageSize,
                    totalCount,
                    continuationToken = nextToken,
                    hasMore = nextToken != null
                }
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var document = await _documentService.GetDocumentAsync(id, userId);
            if (document == null) return NotFound();

            return Ok(document);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            await _documentService.DeleteDocumentAsync(id, userId);
            return NoContent();
        }

        [HttpGet("{id}/status")]
        public async Task<IActionResult> GetStatus(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var document = await _documentService.GetDocumentAsync(id, userId);
            if (document == null) return NotFound();

            return Ok(new
            {
                id = document.Id,
                extractionStatus = document.ExtractionStatus,
                extractionProgress = document.ExtractionProgress,
                hskLevel = document.HskLevel,
                wordCount = document.ExtractedWords.Count,
                tags = document.Tags
            });
        }

        [HttpGet("{id}/audio-url")]
        public async Task<IActionResult> GetAudioUrl(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var document = await _documentService.GetDocumentAsync(id, userId);
            if (document == null) return NotFound();

            if (document.FileType != "audio")
                return BadRequest(new { message = "Document is not an audio file." });

            var sasUrl = await _blobStorage.GenerateSasUrlAsync(
                document.BlobPath, "audios", expiryMinutes: 60);

            return Ok(new { url = sasUrl, expiresIn = 3600 });
        }
    }
}
