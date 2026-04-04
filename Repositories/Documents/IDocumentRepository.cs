using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Documents
{
    public interface IDocumentRepository
    {
        Task<Document> CreateAsync(Document document);
        Task<Document?> GetByIdAsync(string id, string userId);
        Task<(List<Document> items, string? continuationToken, int totalCount)> GetByUserIdAsync(
            string userId,
            int pageSize,
            string? continuationToken,
            string? fileTypeFilter,
            int? hskLevelFilter,
            string? searchQuery);
        Task<Document> UpdateAsync(Document document);
        Task DeleteAsync(string id, string userId);
        Task<int> GetTotalCountAsync(string userId, string? fileTypeFilter, string? searchQuery);
    }
}
