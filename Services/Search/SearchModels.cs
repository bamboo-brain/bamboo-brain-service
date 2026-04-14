using Azure.Search.Documents.Indexes;

namespace BambooBrain_Service.Services.Search
{
    public class WordSearchDocument
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        public string Id { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string UserId { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string DocumentId { get; set; } = string.Empty;

        [SearchableField(IsFilterable = true)]
        public string Word { get; set; } = string.Empty;

        [SearchableField]
        public string Pinyin { get; set; } = string.Empty;

        [SearchableField]
        public string Meaning { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public int? HskLevel { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public int Frequency { get; set; }

        [SimpleField(IsFilterable = true)]
        public string DocumentTitle { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string DocumentType { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public DateTimeOffset IndexedAt { get; set; } = DateTimeOffset.UtcNow;

        [VectorSearchField(VectorSearchDimensions = 1536,
            VectorSearchProfileName = "hnsw-config")]
        public float[]? MeaningVector { get; set; }
    }

    public class ChunkSearchDocument
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        public string Id { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string UserId { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string DocumentId { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string DocumentTitle { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public string DocumentType { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public int ChunkIndex { get; set; }

        [SearchableField]
        public string Content { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        public int? HskLevel { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public DateTimeOffset IndexedAt { get; set; } = DateTimeOffset.UtcNow;

        [VectorSearchField(VectorSearchDimensions = 1536,
            VectorSearchProfileName = "hnsw-config")]
        public float[]? ContentVector { get; set; }
    }
}
