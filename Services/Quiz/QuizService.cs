using Azure;
using Azure.AI.OpenAI;
using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Documents;
using BambooBrain_Service.Repositories.Flashcards;
using BambooBrain_Service.Repositories.Quiz;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BambooBrain_Service.Services.Quiz
{
    public class QuizService : IQuizService
    {
        private readonly IQuizRepository _sessions;
        private readonly IDocumentRepository _documents;
        private readonly IFlashcardRepository _decks;
        private readonly IConfiguration _config;
        private readonly AzureOpenAIClient _aiClient;
        private readonly ILogger<QuizService> _logger;

        public QuizService(
            IQuizRepository sessions,
            IDocumentRepository documents,
            IFlashcardRepository decks,
            IConfiguration config,
            ILogger<QuizService> logger)
        {
            _sessions = sessions;
            _documents = documents;
            _decks = decks;
            _logger = logger;
            _config = config;

            _aiClient = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!),
                new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            );
        }

        // ── Generate quiz ──────────────────────────────────────────────────────

        public async Task<QuizSession> GenerateQuizAsync(
            string userId, GenerateQuizRequest request)
        {
            // Validate source
            if (string.IsNullOrEmpty(request.DocumentId) &&
                string.IsNullOrEmpty(request.DeckId))
                throw new InvalidOperationException(
                    "Either DocumentId or DeckId is required.");

            if (request.QuestionCount < 1 || request.QuestionCount > 50)
                throw new InvalidOperationException(
                    "QuestionCount must be between 1 and 50.");

            // Get words from source
            List<ExtractedWord> words;
            string sourceName;
            string sourceType;
            string sourceId;

            if (!string.IsNullOrEmpty(request.DocumentId))
            {
                var doc = await _documents.GetByIdAsync(request.DocumentId, userId)
                    ?? throw new InvalidOperationException("Document not found.");

                if (doc.ExtractionStatus != "ready")
                    throw new InvalidOperationException(
                        "Document extraction is not complete.");

                if (!doc.ExtractedWords.Any())
                    throw new InvalidOperationException(
                        "No words found in this document.");

                words = doc.ExtractedWords;
                sourceName = doc.FileName;
                sourceType = "document";
                sourceId = request.DocumentId;
            }
            else
            {
                var deck = await _decks.GetByIdAsync(request.DeckId!, userId)
                    ?? throw new InvalidOperationException("Deck not found.");

                // Convert flashcards to ExtractedWord format
                words = deck.Cards.Select(c => new ExtractedWord
                {
                    Word = c.Word,
                    Pinyin = c.Pinyin,
                    Meaning = c.Meaning,
                    HskLevel = c.HskLevel,
                    Frequency = 1
                }).ToList();

                sourceName = deck.Name;
                sourceType = "deck";
                sourceId = request.DeckId!;
            }

            // Filter by HSK level if specified
            if (request.HskLevel.HasValue)
                words = words.Where(w => w.HskLevel == request.HskLevel).ToList();

            // Deduplicate
            words = words
                .GroupBy(w => w.Word.Trim())
                .Select(g => g.OrderByDescending(w => w.Frequency).First())
                .ToList();

            if (!words.Any())
                throw new InvalidOperationException(
                    "No words available for the selected filters.");

            // Shuffle and limit words pool
            var rng = new Random();
            var wordPool = words.OrderBy(_ => rng.Next()).ToList();

            // Generate questions
            var questions = await GenerateQuestionsAsync(
                wordPool, request.QuestionCount, request.Types);

            var session = new QuizSession
            {
                UserId = userId,
                SourceType = sourceType,
                SourceId = sourceId,
                SourceName = sourceName,
                Questions = questions,
                Status = "in-progress"
            };

            return await _sessions.CreateAsync(session);
        }

        // ── Submit answer ──────────────────────────────────────────────────────

        public async Task<QuizSession> SubmitAnswerAsync(
            string sessionId, string userId, SubmitAnswerRequest request)
        {
            var session = await _sessions.GetByIdAsync(sessionId, userId)
                ?? throw new InvalidOperationException("Session not found.");

            if (session.Status == "completed")
                throw new InvalidOperationException("Quiz is already completed.");

            var question = session.Questions.FirstOrDefault(
                q => q.Id == request.QuestionId)
                ?? throw new InvalidOperationException("Question not found.");

            // Check if already answered
            if (session.Answers.Any(a => a.QuestionId == request.QuestionId))
                throw new InvalidOperationException("Question already answered.");

            // Grade the answer
            var isCorrect = GradeAnswer(question, request.UserAnswer);

            session.Answers.Add(new QuizAnswer
            {
                QuestionId = request.QuestionId,
                UserAnswer = request.UserAnswer,
                IsCorrect = isCorrect,
                TimeSpentSeconds = request.TimeSpentSeconds,
                AnsweredAt = DateTime.UtcNow
            });

            return await _sessions.UpdateAsync(session);
        }

        // ── Complete quiz ──────────────────────────────────────────────────────

        public async Task<QuizSession> CompleteQuizAsync(
            string sessionId, string userId)
        {
            var session = await _sessions.GetByIdAsync(sessionId, userId)
                ?? throw new InvalidOperationException("Session not found.");

            session.Status = "completed";
            session.TimeCompleted = DateTime.UtcNow;
            session.Score = session.TotalQuestions > 0
                ? Math.Round((double)session.CorrectAnswers / session.TotalQuestions * 100, 1)
                : 0;

            return await _sessions.UpdateAsync(session);
        }

        // ── Get sessions ───────────────────────────────────────────────────────

        public async Task<List<QuizSession>> GetSessionsAsync(string userId)
            => await _sessions.GetByUserIdAsync(userId);

        public async Task<QuizSession?> GetSessionAsync(
            string sessionId, string userId)
            => await _sessions.GetByIdAsync(sessionId, userId);

        public async Task DeleteSessionAsync(string sessionId, string userId)
            => await _sessions.DeleteAsync(sessionId, userId);

        // ── Stats ──────────────────────────────────────────────────────────────

        public async Task<object> GetStatsAsync(string userId)
        {
            var sessions = await _sessions.GetByUserIdAsync(userId, 100);
            var completed = sessions.Where(s => s.Status == "completed").ToList();

            return new
            {
                totalQuizzesTaken = completed.Count,
                averageScore = completed.Any()
                    ? Math.Round(completed.Average(s => s.Score ?? 0), 1)
                    : 0,
                bestScore = completed.Any()
                    ? completed.Max(s => s.Score ?? 0)
                    : 0,
                totalQuestionsAnswered = completed.Sum(s => s.TotalQuestions),
                correctAnswers = completed.Sum(s => s.CorrectAnswers),
                accuracyPercentage = completed.Sum(s => s.TotalQuestions) > 0
                    ? Math.Round(
                        (double)completed.Sum(s => s.CorrectAnswers) /
                        completed.Sum(s => s.TotalQuestions) * 100, 1)
                    : 0,
                recentSessions = completed.Take(10).Select(s => new
                {
                    s.Id,
                    s.SourceName,
                    s.SourceType,
                    s.Score,
                    s.TotalQuestions,
                    s.CorrectAnswers,
                    s.TimeCompleted,
                    s.CreatedAt
                })
            };
        }

        // ── Question generation ────────────────────────────────────────────────

        private async Task<List<QuizQuestion>> GenerateQuestionsAsync(
            List<ExtractedWord> words,
            int count,
            List<string> types)
        {
            var questions = new List<QuizQuestion>();
            var rng = new Random();

            // Distribute question types evenly
            var typeQueue = new Queue<string>(
                Enumerable.Range(0, count)
                    .Select(i => types[i % types.Count])
                    .OrderBy(_ => rng.Next())
            );

            // Take only as many words as needed
            var selectedWords = words.Take(Math.Min(count, words.Count)).ToList();

            // If fewer words than questions, repeat words with different question types
            while (selectedWords.Count < count)
                selectedWords.Add(words[rng.Next(words.Count)]);

            // Batch generate with GPT-4o (10 questions per batch)
            var batches = selectedWords
                .Zip(typeQueue.ToList(), (word, type) => (word, type))
                .Chunk(10);

            foreach (var batch in batches)
            {
                var batchQuestions = await GenerateQuestionBatchAsync(
                    batch.ToList(), words);
                questions.AddRange(batchQuestions);
            }

            // Shuffle final list
            return questions.OrderBy(_ => rng.Next()).Take(count).ToList();
        }

        private async Task<List<QuizQuestion>> GenerateQuestionBatchAsync(
            List<(ExtractedWord word, string type)> batch,
            List<ExtractedWord> allWords)
        {
            var wordData = batch.Select(b => new
            {
                word = b.word.Word,
                pinyin = b.word.Pinyin,
                meaning = b.word.Meaning,
                hskLevel = b.word.HskLevel,
                type = b.type
            });

            // Build distractor pool from all available words
            var distractors = allWords
                .Select(w => w.Meaning)
                .Distinct()
                .ToList();

            var prompt = $$"""
            Generate quiz questions for Chinese vocabulary learning.
            For each word below, create a question of the specified type.

            Words to quiz:
            {{System.Text.Json.JsonSerializer.Serialize(wordData)}}

            All available meanings for wrong options (distractors):
            {{System.Text.Json.JsonSerializer.Serialize(distractors.Take(30))}}

            Question type rules:
            - "multiple-choice": Ask the meaning. Provide 4 options (1 correct + 3 wrong from distractors). Question: "What does [word] mean?"
            - "fill-in-blank": Show a sentence with the word blanked out. Question: "Fill in the blank: [sentence with ___]". CorrectAnswer is the Chinese word.
            - "tone-identification": Show the word without tone marks. Question: "Select the correct tones for: [word without tones]". Options are 4 different pinyin tone combinations.
            - "listening": The question is the meaning in English. User must identify the Chinese word. Question: "Which word means: [meaning]?". Options are 4 Chinese words.

            Return ONLY a JSON array. No markdown. Each item must have:
            - type: the question type
            - question: the question text
            - word: the Chinese word being tested
            - pinyin: pronunciation with tone marks
            - pinyinWithoutTones: pinyin without tone marks (for tone-identification only, e.g. "bei jing")
            - options: array of 4 strings (for all types)
            - correctAnswer: the correct option string (must exactly match one of the options)
            - explanation: brief explanation of the answer
            - hskLevel: HSK level number or null
            - audioText: text to read aloud for listening type (the Chinese word), null for others

            Example:
            [
              {
                "type": "multiple-choice",
                "question": "What does 北京 mean?",
                "word": "北京",
                "pinyin": "Běijīng",
                "pinyinWithoutTones": null,
                "options": ["Beijing, capital of China", "Shanghai, largest city", "Guangzhou, southern city", "Chengdu, western city"],
                "correctAnswer": "Beijing, capital of China",
                "explanation": "北京 (Běijīng) is the capital city of China.",
                "hskLevel": 1,
                "audioText": null
              }
            ]
            """;

            var chatClient = _aiClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage("You are a Chinese language teacher. Always respond with valid JSON only."),
                    new UserChatMessage(prompt)
                        },
                        new ChatCompletionOptions
                        {
                            MaxOutputTokenCount = 4000,
                            Temperature = 0.4f
                        }
            );

            var content = response.Value.Content[0].Text
                .Replace("```json", "").Replace("```", "").Trim();

            try
            {
                var questions = JsonSerializer.Deserialize<List<QuizQuestion>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new();

                // Validate each question has proper options and correctAnswer
                return questions.Where(q =>
                    q.Options.Count >= 2 &&
                    q.Options.Contains(q.CorrectAnswer)
                ).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to parse quiz batch: {Error}", ex.Message);
                return new();
            }
        }

        // ── Answer grading ─────────────────────────────────────────────────────

        private static bool GradeAnswer(QuizQuestion question, string userAnswer)
        {
            var correct = question.CorrectAnswer.Trim().ToLower();
            var given = userAnswer.Trim().ToLower();

            return question.Type switch
            {
                // Fill-in-blank: accept exact match or close match
                "fill-in-blank" => correct == given ||
                                   NormalizeChineseText(correct) ==
                                   NormalizeChineseText(given),

                // Tone-identification: exact pinyin match
                "tone-identification" => correct == given,

                // All others: exact match (user selected from options)
                _ => correct == given
            };
        }

        private static string NormalizeChineseText(string text)
        {
            // Remove spaces and punctuation for loose matching
            return Regex.Replace(text, @"[\s\p{P}]", "").ToLower();
        }
    }
}
