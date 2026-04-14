namespace BambooBrain_Service.Services.Search
{
    public interface IAISearchService
    {
        // Indexing
        Task IndexDocumentAsync(Models.Document document);
        Task DeleteDocumentFromIndexAsync(string documentId, string userId);

        // Search
        Task<DocumentSearchResult> SearchDocumentsAsync(
            string userId, string query, int top = 10,
            string? fileTypeFilter = null, int? hskLevelFilter = null);

        // RAG
        Task<RagResult> SearchChunksForRagAsync(
            string userId, string query, int topChunks = 5, string? documentTitleHint = null);
    }

    public class DocumentSearchResult
    {
        public List<DocumentSearchHit> Hits { get; set; } = new();
        public int TotalCount { get; set; }
        public string Query { get; set; } = string.Empty;
    }

    public class DocumentSearchHit
    {
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public int? HskLevel { get; set; }
        public double Score { get; set; }
        public List<WordHit> TopWords { get; set; } = new();
    }

    public class WordHit
    {
        public string Word { get; set; } = string.Empty;
        public string Pinyin { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public int? HskLevel { get; set; }
    }

    public class RagResult
    {
        public List<RagChunk> Chunks { get; set; } = new();
        public string CombinedContext { get; set; } = string.Empty;
    }

    public class RagChunk
    {
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Score { get; set; }
    }
}
