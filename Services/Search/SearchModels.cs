using Azure.Search.Documents.Indexes;
using System.Text.Json.Serialization;

namespace BambooBrain_Service.Services.Search
{
    public class WordSearchDocument
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        [JsonPropertyName("documentId")]
        public string DocumentId { get; set; } = string.Empty;

        [SearchableField(IsFilterable = true)]
        [JsonPropertyName("word")]
        public string Word { get; set; } = string.Empty;

        [SearchableField]
        [JsonPropertyName("pinyin")]
        public string Pinyin { get; set; } = string.Empty;

        [SearchableField]
        [JsonPropertyName("meaning")]
        public string Meaning { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true, IsSortable = true)]
        [JsonPropertyName("hskLevel")]
        public int? HskLevel { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true)]
        [JsonPropertyName("frequency")]
        public int Frequency { get; set; }

        [SimpleField(IsFilterable = true)]
        [JsonPropertyName("documentTitle")]
        public string DocumentTitle { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        [JsonPropertyName("documentType")]
        public string DocumentType { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true, IsSortable = true)]
        [JsonPropertyName("indexedAt")]
        public DateTimeOffset IndexedAt { get; set; } = DateTimeOffset.UtcNow;

        [VectorSearchField(VectorSearchDimensions = 1536,
            VectorSearchProfileName = "hnsw-profile")]
        [JsonPropertyName("meaningVector")]
        public float[]? MeaningVector { get; set; }
    }

    // Services/Search/SearchModels.cs
    public class ChunkSearchDocument
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        [JsonPropertyName("documentId")]
        public string DocumentId { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        [JsonPropertyName("documentTitle")]
        public string DocumentTitle { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        [JsonPropertyName("documentType")]
        public string DocumentType { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true, IsSortable = true)]
        [JsonPropertyName("chunkIndex")]
        public int ChunkIndex { get; set; }

        [SearchableField]
        [JsonPropertyName("content")]                    // ← force lowercase
        public string Content { get; set; } = string.Empty;

        [SimpleField(IsFilterable = true)]
        [JsonPropertyName("hskLevel")]
        public int? HskLevel { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true)]
        [JsonPropertyName("indexedAt")]
        public DateTimeOffset IndexedAt { get; set; } = DateTimeOffset.UtcNow;

        [VectorSearchField(VectorSearchDimensions = 1536,
            VectorSearchProfileName = "hnsw-profile")]
        [JsonPropertyName("contentVector")]
        public float[]? ContentVector { get; set; }
    }
}
