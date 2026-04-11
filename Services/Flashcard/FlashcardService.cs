using Azure;
using Azure.AI.OpenAI;
using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Documents;
using BambooBrain_Service.Repositories.Flashcards;
using Microsoft.Identity.Client;
using OpenAI.Chat;

namespace BambooBrain_Service.Services.Flashcard
{
    public class FlashcardService : IFlashcardService
    {
        private readonly IFlashcardRepository _decks;
        private readonly IDocumentRepository _documents;
        private readonly AzureOpenAIClient _aiClient;
        private readonly IConfiguration _config;
        private readonly ILogger<FlashcardService> _logger;

        public FlashcardService(
            IFlashcardRepository decks,
            IDocumentRepository documents,
            IConfiguration config,
            ILogger<FlashcardService> logger)
        {
            _decks = decks;
            _documents = documents;
            _config = config;
            _logger = logger;
            _aiClient = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!),
                new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            );
        }

        // ── Create deck manually ───────────────────────────────────────────────

        public async Task<FlashcardDeck> CreateDeckAsync(
            string userId, CreateDeckRequest request)
        {
            var deck = new FlashcardDeck
            {
                UserId = userId,
                Name = request.Name,
                Description = request.Description,
                Tags = request.Tags,
                SourceType = request.SourceType,
                SourceDocumentId = request.SourceDocumentId,
                Cards = request.Cards.Select(c => new Models.Flashcard
                {
                    Word = c.Word,
                    Pinyin = c.Pinyin,
                    Meaning = c.Meaning,
                    ExampleSentence = c.ExampleSentence,
                    ExampleTranslation = c.ExampleTranslation,
                    HskLevel = c.HskLevel
                }).ToList()
            };

            return await _decks.CreateAsync(deck);
        }

        // ── Auto-create deck from extracted document words ─────────────────────

        public async Task<FlashcardDeck> CreateDeckFromDocumentAsync(
            string userId, CreateDeckFromDocumentRequest request)
        {
            var document = await _documents.GetByIdAsync(request.DocumentId, userId)
                ?? throw new InvalidOperationException("Document not found.");

            if (document.ExtractionStatus != "ready")
                throw new InvalidOperationException(
                    "Document extraction is not complete yet.");

            if (!document.ExtractedWords.Any())
                throw new InvalidOperationException(
                    "No words extracted from this document.");

            // Filter words by HSK level if specified
            var words = document.ExtractedWords.AsEnumerable();

            if (request.MinHskLevel.HasValue)
                words = words.Where(w => w.HskLevel >= request.MinHskLevel);

            if (request.MaxHskLevel.HasValue)
                words = words.Where(w => w.HskLevel <= request.MaxHskLevel);

            // Take most frequent words up to MaxCards limit
            var selectedWords = words
                .GroupBy(w => w.Word.Trim())
                .Select(g => g.OrderByDescending(w => w.Frequency).First())
                .OrderByDescending(w => w.Frequency)
                .Take(request.MaxCards ?? 50)
                .ToList();

            // Generate example sentences using GPT-4o
            var cards = await GenerateFlashcardsAsync(selectedWords);

            var deck = new FlashcardDeck
            {
                UserId = userId,
                Name = request.Name.Length > 0
                    ? request.Name
                    : $"{document.FileName} Vocabulary",
                Description = request.Description.Length > 0
                    ? request.Description
                    : $"Extracted from {document.FileName}",
                Tags = document.Tags,
                SourceType = "document",
                SourceDocumentId = request.DocumentId,
                Cards = cards
            };

            return await _decks.CreateAsync(deck);
        }

        // ── Get all decks ──────────────────────────────────────────────────────

        public async Task<List<FlashcardDeck>> GetUserDecksAsync(string userId)
            => await _decks.GetByUserIdAsync(userId);

        public async Task<FlashcardDeck?> GetDeckAsync(string id, string userId)
            => await _decks.GetByIdAsync(id, userId);

        // ── Add card to existing deck ──────────────────────────────────────────

        public async Task<FlashcardDeck> AddCardAsync(
            string deckId, string userId, AddCardRequest request)
        {
            var deck = await _decks.GetByIdAsync(deckId, userId)
                ?? throw new InvalidOperationException("Deck not found.");

            var existingCard = deck.Cards.FirstOrDefault(c => c.Word.Trim() == request.Word.Trim());

            if (existingCard != null)
                throw new InvalidOperationException($"Word '{request.Word}' already exists in this deck.");

            var card = new Models.Flashcard
            {
                Word = request.Word,
                Pinyin = request.Pinyin,
                Meaning = request.Meaning,
                ExampleSentence = request.ExampleSentence,
                ExampleTranslation = request.ExampleTranslation,
                HskLevel = request.HskLevel
            };

            deck.Cards.Add(card);
            return await _decks.UpdateAsync(deck);
        }

        // ── Remove card from deck ──────────────────────────────────────────────

        public async Task<FlashcardDeck> RemoveCardAsync(
            string deckId, string cardId, string userId)
        {
            var deck = await _decks.GetByIdAsync(deckId, userId)
                ?? throw new InvalidOperationException("Deck not found.");

            deck.Cards.RemoveAll(c => c.Id == cardId);
            return await _decks.UpdateAsync(deck);
        }

        // ── Review card (SM-2) ─────────────────────────────────────────────────

        public async Task<FlashcardDeck> ReviewCardAsync(
            string deckId, string userId, ReviewCardRequest request)
        {
            var deck = await _decks.GetByIdAsync(deckId, userId)
                ?? throw new InvalidOperationException("Deck not found.");

            var card = deck.Cards.FirstOrDefault(c => c.Id == request.CardId)
                ?? throw new InvalidOperationException("Card not found.");

            // Apply SM-2 algorithm
            SM2Algorithm.Calculate(card, request.Grade);

            return await _decks.UpdateAsync(deck);
        }

        // ── Get due cards for today ────────────────────────────────────────────

        public async Task<List<Models.Flashcard>> GetDueCardsAsync(string deckId, string userId)
        {
            var deck = await _decks.GetByIdAsync(deckId, userId)
                ?? throw new InvalidOperationException("Deck not found.");

            return deck.Cards
                .Where(c => c.NextReviewDate <= DateTime.UtcNow)
                .OrderBy(c => c.NextReviewDate)
                .ToList();
        }

        // ── Delete deck ────────────────────────────────────────────────────────

        public async Task DeleteDeckAsync(string id, string userId)
            => await _decks.DeleteAsync(id, userId);

        // ── GPT-4o flashcard generation ────────────────────────────────────────

        private async Task<List<Models.Flashcard>> GenerateFlashcardsAsync(
            List<ExtractedWord> words)
        {
            // Batch words to avoid token limits (20 words per batch)
            var flashcards = new List<Models.Flashcard>();
            var batches = words.Chunk(20);

            foreach (var batch in batches)
            {
                var wordList = string.Join(", ", batch.Select(w => w.Word));

                var prompt = $$"""
                    Create flashcards for these Chinese words: {{wordList}}

                    For each word provide:
                    - word: Chinese characters
                    - pinyin: pronunciation with tone marks
                    - meaning: concise English meaning
                    - exampleSentence: a natural Chinese sentence using the word
                    - exampleTranslation: English translation of the example
                    - hskLevel: HSK level 1-6 or null

                    Return ONLY a JSON array. No markdown.
                    Example: [{
                      "word":"北京",
                      "pinyin":"Běijīng",
                      "meaning":"Beijing, capital of China",
                      "exampleSentence":"我在北京工作。",
                      "exampleTranslation":"I work in Beijing.",
                      "hskLevel":1
                    }]
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
                                Temperature = 0.3f
                            }
                );

                var content = response.Value.Content[0].Text
                    .Replace("```json", "").Replace("```", "").Trim();

                try
                {
                    var batchCards = System.Text.Json.JsonSerializer
                        .Deserialize<List<Models.Flashcard>>(content,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? new();

                    flashcards.AddRange(batchCards);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to parse flashcard batch: {Error}", ex.Message);
                }
            }

            var deduplicated = flashcards
                .GroupBy(c => c.Word.Trim())
                .Select(g => g.First())
                .ToList();

            return deduplicated;
        }
    }
}
