using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Document
{
    public interface IDocumentService
    {
        Task<Models.Document> UploadDocumentAsync(string userId, IFormFile file);
        Task<(List<Models.Document> items, string? continuationToken, int totalCount)> GetUserDocumentsAsync(
            string userId,
            int pageSize = 10,
            string? continuationToken = null,
            string? fileTypeFilter = null,
            string? searchQuery = null);
        Task<Models.Document?> GetDocumentAsync(string id, string userId);
        Task DeleteDocumentAsync(string id, string userId);
    }
}
