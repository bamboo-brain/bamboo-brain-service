using Azure;
using Azure.AI.OpenAI;
using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Speaking;
using OpenAI.Chat;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace BambooBrain_Service.Services.Speaking
{
    public class SpeakingService : ISpeakingService
    {
        private readonly ISpeakingRepository _sessions;
        private readonly ISpeechService _speech;
        private readonly IConfiguration _config;
        private readonly AzureOpenAIClient _aiClient;
        private readonly ILogger<SpeakingService> _logger;

        public SpeakingService(
            ISpeakingRepository sessions,
            ISpeechService speech,
            IConfiguration config,
            ILogger<SpeakingService> logger)
        {
            _sessions = sessions;
            _speech = speech;
            _logger = logger;
            _config = config;

            _aiClient = new AzureOpenAIClient(
                new Uri(config["AzureOpenAI:Endpoint"]!),
                new AzureKeyCredential(config["AzureOpenAI:ApiKey"]!),
                new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            );
        }

        // ── Start session ──────────────────────────────────────────────────────

        public async Task<SpeakingSession> StartSessionAsync(
            string userId, StartSessionRequest request)
        {
            var session = new SpeakingSession
            {
                UserId = userId,
                Topic = request.Topic,
                TopicDescription = request.TopicDescription,
                HskLevel = request.HskLevel,
                Status = "active"
            };

            // Generate AI opening line
            var openingTurn = await GenerateAITurnAsync(session, null);
            session.Turns.Add(openingTurn);

            return await _sessions.CreateAsync(session);
        }

        // ── Process audio turn ─────────────────────────────────────────────────

        public async Task<ConversationTurn> ProcessTurnAsync(
            string sessionId, string userId, ProcessTurnRequest request)
        {
            var session = await _sessions.GetByIdAsync(sessionId, userId)
                ?? throw new InvalidOperationException("Session not found.");

            if (session.Status == "completed")
                throw new InvalidOperationException("Session is already completed.");

            // Step 1 — Transcribe user's audio
            var sttResult = await _speech.RecognizeAsync(
                request.AudioBase64, request.MimeType);

            if (!sttResult.Success || string.IsNullOrWhiteSpace(sttResult.Text))
                throw new InvalidOperationException(
                    sttResult.Error ?? "Could not recognize speech.");

            return await ProcessUserTextAsync(
                session, sttResult.Text, sttResult.AccuracyScore);
        }

        // ── Process text turn (fallback) ───────────────────────────────────────

        public async Task<ConversationTurn> ProcessTextTurnAsync(
            string sessionId, string userId, TextTurnRequest request)
        {
            var session = await _sessions.GetByIdAsync(sessionId, userId)
                ?? throw new InvalidOperationException("Session not found.");

            if (session.Status == "completed")
                throw new InvalidOperationException("Session is already completed.");

            return await ProcessUserTextAsync(session, request.Text, 85.0);
        }

        // ── Shared text processing ─────────────────────────────────────────────

        private async Task<ConversationTurn> ProcessUserTextAsync(
            SpeakingSession session, string userText, double accuracyScore)
        {
            // Step 2 — Analyze tone corrections
            var toneCorrections = await AnalyzeTonesAsync(userText);

            // Step 3 — Get pinyin + translation for user text
            var userAnalysis = await AnalyzeUserTextAsync(userText);

            // Step 4 — Build user turn
            var userTurn = new ConversationTurn
            {
                Role = "user",
                Text = userText,
                Pinyin = userAnalysis.Pinyin,
                Translation = userAnalysis.Translation,
                ToneCorrections = toneCorrections,
                AccuracyScore = accuracyScore
            };
            session.Turns.Add(userTurn);

            // Step 5 — Generate AI response
            var aiTurn = await GenerateAITurnAsync(session, userText);
            session.Turns.Add(aiTurn);

            // Step 6 — Update top vocabulary
            UpdateTopVocabulary(session, userText, userAnalysis.Words);

            await _sessions.UpdateAsync(session);

            // Return both turns together
            // Frontend needs both: the user turn result + AI response
            return aiTurn; // frontend already has user turn optimistically
        }

        // ── End session ────────────────────────────────────────────────────────

        public async Task<SpeakingSession> EndSessionAsync(
            string sessionId, string userId, EndSessionRequest request)
        {
            var session = await _sessions.GetByIdAsync(sessionId, userId)
                ?? throw new InvalidOperationException("Session not found.");

            session.Status = "completed";
            session.EndedAt = DateTime.UtcNow;
            session.DurationSeconds = request.DurationSeconds;

            // Generate session insights
            session.Insights = CalculateInsights(session);

            return await _sessions.UpdateAsync(session);
        }

        // ── Get sessions ───────────────────────────────────────────────────────

        public async Task<List<SpeakingSession>> GetSessionsAsync(string userId)
            => await _sessions.GetByUserIdAsync(userId);

        public async Task<SpeakingSession?> GetSessionAsync(
            string sessionId, string userId)
            => await _sessions.GetByIdAsync(sessionId, userId);

        public async Task DeleteSessionAsync(string sessionId, string userId)
            => await _sessions.DeleteAsync(sessionId, userId);

        // ── Stats ──────────────────────────────────────────────────────────────

        public async Task<object> GetStatsAsync(string userId)
        {
            var sessions = await _sessions.GetByUserIdAsync(userId, 100);
            var completed = sessions.Where(s => s.Status == "completed").ToList();

            var avgAccuracy = completed.Any()
                ? completed
                    .Where(s => s.Insights != null)
                    .Select(s => s.Insights!.AccuracyScore)
                    .DefaultIfEmpty(0)
                    .Average()
                : 0;

            var totalSeconds = completed.Sum(s => s.DurationSeconds);
            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;

            return new
            {
                avgPronunciationScore = Math.Round(avgAccuracy, 1),
                activeSpeakingTime = $"{hours}h {minutes}m",
                conversationsCompleted = completed.Count,
                recentSessions = completed.Take(20).Select(s => new
                {
                    s.Id,
                    s.Topic,
                    s.HskLevel,
                    accuracy = s.Insights?.AccuracyScore ?? 0,
                    fluency = s.Insights?.Fluency ?? "N/A",
                    duration = FormatDuration(s.DurationSeconds),
                    date = s.CreatedAt
                })
            };
        }

        // ── AI turn generation ─────────────────────────────────────────────────

        private async Task<ConversationTurn> GenerateAITurnAsync(
            SpeakingSession session, string? lastUserText)
        {
            // Build conversation history for context
            var history = session.Turns
                .TakeLast(6) // keep last 3 exchanges for context
                .Select(t => $"{(t.Role == "ai" ? "Master Ling" : "Student")}: {t.Text}")
                .ToList();

            var isOpening = !session.Turns.Any();

            var systemPrompt = $"""
            You are Master Ling, a friendly and encouraging Chinese language tutor.
            You are having a conversation practice session on the topic: "{session.Topic}".
            Topic context: {session.TopicDescription}
            Student's HSK level: {session.HskLevel}

            Rules:
            - ALWAYS respond in Chinese (Mandarin)
            - Keep responses appropriate for HSK level {session.HskLevel}
            - Be encouraging and natural
            - Ask follow-up questions to keep the conversation flowing
            - Keep responses concise (1-3 sentences)
            - Do NOT include pinyin or translations in your response
            """;

            var userPrompt = isOpening
                ? $"Start the conversation about '{session.Topic}'. Ask an opening question."
                : $"""
                Conversation so far:
                {string.Join("\n", history)}

                Student just said: "{lastUserText}"
                Respond naturally and ask a follow-up question.
                """;

            var chatClient = _aiClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                        },
                        new ChatCompletionOptions
                        {
                            MaxOutputTokenCount = 200,
                            Temperature = 0.7f
                        }
            );

            var aiText = response.Value.Content[0].Text
                .Replace("```json", "").Replace("```", "").Trim();

            // Get pinyin + translation for AI response
            var analysis = await AnalyzeUserTextAsync(aiText);

            // Generate TTS audio for the AI response
            var audioUrl = await _speech.SynthesizeAsync(
                aiText, session.UserId, session.Id);

            return new ConversationTurn
            {
                Role = "ai",
                Text = aiText,
                Pinyin = analysis.Pinyin,
                Translation = analysis.Translation,
                AudioUrl = audioUrl
            };
        }

        // ── Tone analysis ──────────────────────────────────────────────────────

        private async Task<List<ToneCorrection>> AnalyzeTonesAsync(string text)
        {
            var prompt = $$"""
                Analyze the pronunciation of this Chinese text and identify any tone errors
                that a learner commonly makes.

                Text: {{text}}

                Return a JSON array of tone corrections (empty array [] if no issues).
                Each correction:
                - word: the Chinese word with a tone issue
                - correctPinyin: the correct pinyin with tone marks
                - spokenPinyin: what the student likely said (common mistake)
                - message: friendly explanation e.g. "měishì (3rd tone) was pronounced as 2nd tone"

                Return at most 2 corrections. Return [] if the text is simple/correct.
                Example: [{"word":"美式","correctPinyin":"měishì","spokenPinyin":"méishì","message":"měishì (3rd tone) was pronounced slightly as 2nd tone"}]
                """;

            var chatClient = _aiClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage("You are a Chinese pronunciation expert. Always respond with valid JSON only."),
                    new UserChatMessage(prompt)
                        },
                        new ChatCompletionOptions
                        {
                            MaxOutputTokenCount = 500,
                            Temperature = 0.2f
                        }
            );

            var content = response.Value.Content[0].Text
                .Replace("```json", "").Replace("```", "").Trim();

            try
            {
                return JsonSerializer.Deserialize<List<ToneCorrection>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new();
            }
            catch
            {
                return new();
            }
        }

        // ── Text analysis (pinyin + translation + word extraction) ─────────────

        private async Task<TextAnalysis> AnalyzeUserTextAsync(string text)
        {
            var prompt = $$"""
                Analyze this Chinese text:
                "{{text}}"

                Return JSON with:
                - pinyin: full pinyin with tone marks for the entire sentence
                - translation: natural English translation
                - words: array of key vocabulary [{word, pinyin, meaning}] (max 5 important words)

                Example:
                {
                  "pinyin": "Wǒ yào yī bēi měishì kāfēi.",
                  "translation": "I would like an Americano coffee.",
                  "words": [{"word":"美式","pinyin":"měishì","meaning":"Americano"}]
                }
                """;

            var chatClient = _aiClient.GetChatClient(_config["AzureOpenAI:DeploymentName"]!);

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage("You are a Chinese language expert. Always respond with valid JSON only."),
                    new UserChatMessage(prompt)
                        },
                        new ChatCompletionOptions
                        {
                            MaxOutputTokenCount = 400,
                            Temperature = 0.1f
                        }
            );

            var content = response.Value.Content[0].Text
                .Replace("```json", "").Replace("```", "").Trim();

            try
            {
                return JsonSerializer.Deserialize<TextAnalysis>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new TextAnalysis { Pinyin = "", Translation = "" };
            }
            catch
            {
                return new TextAnalysis { Pinyin = "", Translation = text };
            }
        }

        // ── Vocabulary tracking ────────────────────────────────────────────────

        private static void UpdateTopVocabulary(
            SpeakingSession session, string text, List<VocabularyWord> words)
        {
            foreach (var word in words)
            {
                var existing = session.TopVocabulary
                    .FirstOrDefault(v => v.Word == word.Word);

                if (existing != null)
                {
                    existing.UsageCount++;
                }
                else
                {
                    session.TopVocabulary.Add(new VocabularyItem
                    {
                        Word = word.Word,
                        Pinyin = word.Pinyin,
                        Meaning = word.Meaning,
                        UsageCount = 1
                    });
                }
            }

            // Keep top 10 by usage
            session.TopVocabulary = session.TopVocabulary
                .OrderByDescending(v => v.UsageCount)
                .Take(10)
                .ToList();
        }

        // ── Session insights calculation ───────────────────────────────────────

        private static SessionInsights CalculateInsights(SpeakingSession session)
        {
            var userTurns = session.Turns.Where(t => t.Role == "user").ToList();
            var accuracyScores = userTurns
                .Where(t => t.AccuracyScore.HasValue)
                .Select(t => t.AccuracyScore!.Value)
                .ToList();

            var avgAccuracy = accuracyScores.Any()
                ? Math.Round(accuracyScores.Average(), 1)
                : 0;

            var toneErrors = userTurns.Sum(t => t.ToneCorrections.Count);

            return new SessionInsights
            {
                AccuracyScore = avgAccuracy,
                Fluency = avgAccuracy switch
                {
                    >= 90 => "Excellent",
                    >= 75 => "Great",
                    >= 60 => "Good",
                    _ => "Needs Work"
                },
                TotalTurns = session.Turns.Count,
                UserTurns = userTurns.Count,
                ToneErrorCount = toneErrors,
                VocabularyCount = session.TopVocabulary.Count,
                Duration = FormatDuration(session.DurationSeconds)
            };
        }

        private static string FormatDuration(int seconds)
        {
            var m = seconds / 60;
            var s = seconds % 60;
            return $"{m}:{s:D2}";
        }
    }

    // Internal models
    public class TextAnalysis
    {
        public string Pinyin { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public List<VocabularyWord> Words { get; set; } = new();
    }

    public class VocabularyWord
    {
        public string Word { get; set; } = string.Empty;
        public string Pinyin { get; set; } = string.Empty;
        public string Meaning { get; set; } = string.Empty;
    }
}
