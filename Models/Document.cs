using Newtonsoft.Json;

namespace BambooBrain_Service.Models
{
    public class Document
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonProperty("fileType")]
        public string FileType { get; set; } = string.Empty; // "pdf" | "video" | "audio" | "ppt"

        [JsonProperty("mimeType")]
        public string MimeType { get; set; } = string.Empty;

        [JsonProperty("fileSize")]
        public long FileSize { get; set; }

        [JsonProperty("blobUrl")]
        public string BlobUrl { get; set; } = string.Empty;

        [JsonProperty("blobPath")]
        public string BlobPath { get; set; } = string.Empty;

        [JsonProperty("hskLevel")]
        public int? HskLevel { get; set; }

        [JsonProperty("pageCount")]
        public int? PageCount { get; set; }

        [JsonProperty("duration")]
        public string? Duration { get; set; } // for video/audio e.g. "15:20"

        [JsonProperty("extractionStatus")]
        public string ExtractionStatus { get; set; } = "pending"; // "pending" | "analyzing" | "ready" | "failed"

        [JsonProperty("extractionProgress")]
        public int ExtractionProgress { get; set; } = 0; // 0-100

        [JsonProperty("extractedText")]
        public string? ExtractedText { get; set; }

        [JsonProperty("extractedWords")]
        public List<ExtractedWord> ExtractedWords { get; set; } = new();

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonProperty("uploadedAt")]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ExtractedWord
    {
        [JsonProperty("word")]
        public string Word { get; set; } = string.Empty;

        [JsonProperty("pinyin")]
        public string Pinyin { get; set; } = string.Empty;

        [JsonProperty("meaning")]
        public string Meaning { get; set; } = string.Empty;

        [JsonProperty("hskLevel")]
        public int? HskLevel { get; set; }

        [JsonProperty("frequency")]
        public int Frequency { get; set; } = 1;
    }
}
