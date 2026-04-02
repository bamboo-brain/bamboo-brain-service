using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Document
{
    public interface IDocumentService
    {
        Task<Models.Document> UploadDocumentAsync(
            string userId, IFormFile file);
        Task<List<Models.Document>> GetUserDocumentsAsync(string userId);
        Task<Models.Document?> GetDocumentAsync(string id, string userId);
        Task DeleteDocumentAsync(string id, string userId);
    }
}
