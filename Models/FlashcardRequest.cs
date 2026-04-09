namespace BambooBrain_Service.Models
{
    public class CreateDeckRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string SourceType { get; set; } = "manual";
        public string? SourceDocumentId { get; set; }
        public List<CreateCardRequest> Cards { get; set; } = new();
    }

    public class CreateDeckFromDocumentRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public int? MaxCards { get; set; } = 50;         // limit cards per deck
        public int? MinHskLevel { get; set; }            // filter by HSK level
        public int? MaxHskLevel { get; set; }
    }

    public class CreateCardRequest
    {
        public string Word { get; set; } = string.Empty;
        public string Pinyin { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? ExampleTranslation { get; set; }
        public int? HskLevel { get; set; }
    }

    public class AddCardRequest
    {
        public string Word { get; set; } = string.Empty;
        public string Pinyin { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? ExampleTranslation { get; set; }
        public int? HskLevel { get; set; }
    }

    public class ReviewCardRequest
    {
        public string CardId { get; set; } = string.Empty;
        public int Grade { get; set; }  // 0-5 (0=blackout, 5=perfect)
    }
}
