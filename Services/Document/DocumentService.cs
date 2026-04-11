using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Documents;
using BambooBrain_Service.Services.BlobStorage;
using BambooBrain_Service.Services.Extraction;
using BambooBrain_Service.Services.Notifications;

namespace BambooBrain_Service.Services.Document
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documents;
        private readonly IBlobStorageService _blob;
        private readonly IConfiguration _config;
        private readonly IExtractionService _documentExtraction;
        private readonly INotificationService _notifications;
        private readonly AudioExtractionService _audioExtraction;
        private readonly VideoExtractionService _videoExtraction;
        private readonly ILogger<AudioExtractionService> _logger;

        private static readonly Dictionary<string, (string fileType, string container)> _mimeMap = new()
        {
            ["application/pdf"] = ("pdf", "documents"),
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ("ppt", "documents"),
            ["application/vnd.ms-powerpoint"] = ("ppt", "documents"),
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ("pdf", "documents"),
            ["video/mp4"] = ("video", "videos"),
            ["video/mpeg"] = ("video", "videos"),
            ["video/quicktime"] = ("video", "videos"),
            ["audio/mpeg"] = ("audio", "audios"),
            ["audio/mp4"] = ("audio", "audios"),
            ["audio/wav"] = ("audio", "audios"),
            ["audio/x-m4a"] = ("audio", "audios"),
        };

        public DocumentService(
            IDocumentRepository documents,
            IBlobStorageService blob,
            IExtractionService documentExtraction,
            INotificationService notifications,
            AudioExtractionService audioExtraction,
            VideoExtractionService videoExtraction,
            ILogger<AudioExtractionService> logger,
            IConfiguration config)
        {
            _documents = documents;
            _blob = blob;
            _documentExtraction = documentExtraction;
            _audioExtraction = audioExtraction;
            _videoExtraction = videoExtraction;
            _notifications = notifications;
            _logger = logger;
            _config = config;
        }

        public async Task<Models.Document> UploadDocumentAsync(string userId, IFormFile file)
        {
            // Validate file type
            if (!_mimeMap.TryGetValue(file.ContentType, out var fileInfo))
                throw new InvalidOperationException($"Unsupported file type: {file.ContentType}");

            var (fileType, container) = fileInfo;

            // Upload to Blob Storage
            using var stream = file.OpenReadStream();
            var (blobPath, blobUrl) = await _blob.UploadAsync(
                stream, file.FileName, file.ContentType, container);

            // Create document record in Cosmos DB
            var document = new Models.Document
            {
                UserId = userId,
                FileName = file.FileName,
                FileType = fileType,
                MimeType = file.ContentType,
                FileSize = file.Length,
                BlobPath = blobPath,
                BlobUrl = blobUrl,
                ExtractionStatus = "pending",
            };

            var created = await _documents.CreateAsync(document);

            // Trigger async extraction (fire and forget)
            _ = Task.Run(() => TriggerExtractionAsync(created));

            return created;
        }

        public async Task<(List<Models.Document> items, string? continuationToken, int totalCount)> GetUserDocumentsAsync(
            string userId,
            int pageSize = 10,
            string? continuationToken = null,
            string? fileTypeFilter = null,
            int? hskLevelFilter = null,
            string? searchQuery = null)
        {
            return await _documents.GetByUserIdAsync(
                userId, pageSize, continuationToken, fileTypeFilter, hskLevelFilter, searchQuery);
        }

        public async Task<Models.Document?> GetDocumentAsync(string id, string userId)
            => await _documents.GetByIdAsync(id, userId);

        public async Task DeleteDocumentAsync(string id, string userId)
        {
            var doc = await _documents.GetByIdAsync(id, userId);
            if (doc == null) return;

            // Determine container from file type
            var container = doc.FileType == "video" ? "videos"
                          : doc.FileType == "audio" ? "audios"
                          : "documents";

            await _blob.DeleteAsync(doc.BlobPath, container);
            await _documents.DeleteAsync(id, userId);
        }

        private async Task TriggerExtractionAsync(Models.Document document)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    switch (document.FileType)
                    {
                        case "pdf":
                        case "ppt":
                            await _documentExtraction.ExtractAsync(document);
                            break;
                        case "audio":
                            await _audioExtraction.ExtractAsync(document);
                            break;
                        case "video":                                    // ← add
                            await _videoExtraction.ExtractAsync(document);
                            break;
                        default:
                            _logger.LogWarning("No handler for: {Type}", document.FileType);
                            break;
                    }

                    await _notifications.SendProcessingCompleteAsync(
                        document.UserId,
                        document.Id,
                        document.FileName
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Extraction error for {Id}", document.Id);
                    document.ExtractionStatus = "failed";
                    document.UpdatedAt = DateTime.UtcNow;
                    await _documents.UpdateAsync(document);
                }
            });
        }
    }
}
