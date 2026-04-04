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
            [FromQuery] string? search = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (pageSize < 1 || pageSize > 50)
                return BadRequest(new { message = "pageSize must be between 1 and 50." });

            var (items, nextToken, totalCount) = await _documentService.GetUserDocumentsAsync(
                userId, pageSize, continuationToken, fileType, search);

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

        [HttpGet("{id}/audio")]
        public async Task<IActionResult> GetAudio(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var document = await _documentService.GetDocumentAsync(id, userId);
            if (document == null) return NotFound();

            if (document.FileType != "audio")
                return BadRequest(new { message = "Document is not an audio file." });

            // Download from blob
            var stream = await _blobStorage.DownloadAsync(document.BlobPath, "audios");

            // Copy to MemoryStream to support seeking (required for range requests)
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var contentType = document.MimeType ?? "audio/mpeg";
            var fileName = document.FileName;
            var fileSize = memoryStream.Length;

            // Handle byte-range requests for seeking
            if (Request.Headers.TryGetValue("Range", out var rangeHeader))
            {
                var range = rangeHeader.ToString();

                // Parse range: "bytes=start-end"
                var rangeValue = range.Replace("bytes=", "").Trim();
                var parts = rangeValue.Split('-');

                var start = long.Parse(parts[0]);
                var end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
                    ? long.Parse(parts[1])
                    : fileSize - 1;

                // Clamp end to file size
                end = Math.Min(end, fileSize - 1);
                var length = end - start + 1;

                memoryStream.Seek(start, SeekOrigin.Begin);
                var buffer = new byte[length];
                await memoryStream.ReadAsync(buffer, 0, (int)length);

                Response.StatusCode = 206; // Partial Content
                Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{fileSize}");
                Response.Headers.Append("Accept-Ranges", "bytes");
                Response.Headers.Append("Content-Length", length.ToString());
                Response.Headers.Append("Content-Type", contentType);
                Response.Headers.Append("Cache-Control", "no-cache");

                return File(buffer, contentType);
            }

            // Full file response
            Response.Headers.Append("Accept-Ranges", "bytes");
            Response.Headers.Append("Content-Length", fileSize.ToString());
            Response.Headers.Append("Cache-Control", "no-cache");

            return File(memoryStream, contentType, fileName, enableRangeProcessing: true);
        }
    }
}
