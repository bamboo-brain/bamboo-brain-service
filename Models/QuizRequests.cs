namespace BambooBrain_Service.Models
{
    public class GenerateQuizRequest
    {
        public string? DocumentId { get; set; }
        public string? DeckId { get; set; }
        public int QuestionCount { get; set; } = 10;
        public List<string> Types { get; set; } = new()
        {
            "multiple-choice",
            "fill-in-blank",
            "tone-identification",
            "listening"
        };
        public int? HskLevel { get; set; }
    }

    public class SubmitAnswerRequest
    {
        public string QuestionId { get; set; } = string.Empty;
        public string UserAnswer { get; set; } = string.Empty;
        public int TimeSpentSeconds { get; set; }
    }
}
