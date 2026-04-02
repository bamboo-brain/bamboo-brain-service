using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Documents
{
    public interface IDocumentRepository
    {
        Task<Document> CreateAsync(Document document);
        Task<Document?> GetByIdAsync(string id, string userId);
        Task<List<Document>> GetByUserIdAsync(string userId);
        Task<Document> UpdateAsync(Document document);
        Task DeleteAsync(string id, string userId);
    }
}
