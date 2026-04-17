using Azure.Search.Documents.Models;
using Azure.Search.Documents;
using Azure;
using System.Text;

namespace BambooBrain_Service.Services.Search
{
    public class AISearchService : IAISearchService
    {
        private readonly SearchClient _wordsClient;
        private readonly SearchClient _chunksClient;
        private readonly IEmbeddingService _embeddings;
        private readonly IConfiguration _config;
        private readonly ILogger<AISearchService> _logger;

        private const int ChunkSize = 500;   // chars per chunk
        private const int ChunkOverlap = 50; // overlap between chunks

        public AISearchService(
            IConfiguration config,
            IEmbeddingService embeddings,
            ILogger<AISearchService> logger)
        {
            _config = config;
            _embeddings = embeddings;
            _logger = logger;

            var endpoint = new Uri(config["AzureSearch:Endpoint"]!);
            var credential = new AzureKeyCredential(config["AzureSearch:AdminKey"]!);

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            var searchClientOptions = new SearchClientOptions
            {
                Serializer = new Azure.Search.Documents.Indexes.FieldBuilder()
                    .Build(typeof(ChunkSearchDocument))
                    .ToString() == null
                        ? null
                        : new Azure.Core.Serialization.JsonObjectSerializer(jsonOptions)
            };

            _wordsClient = new SearchClient(
                endpoint,
                config["AzureSearch:WordsIndex"]!,
                credential,
                new SearchClientOptions
                {
                    Serializer = new Azure.Core.Serialization.JsonObjectSerializer(
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                        })
                });

            _chunksClient = new SearchClient(
                endpoint,
                config["AzureSearch:ChunksIndex"]!,
                credential,
                new SearchClientOptions
                {
                    Serializer = new Azure.Core.Serialization.JsonObjectSerializer(
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                        })
                });
        }

        public async Task<string> SearchWordsForContextAsync(string userId, string query, int top = 20)
        {
            var queryVector = await _embeddings.GetEmbeddingAsync(query);

            var options = new SearchOptions
            {
                Filter = $"userId eq '{userId}'",
                Size = top,
                Select = { "word", "pinyin", "meaning", "hskLevel",
                   "frequency", "documentTitle" },
            };

            if (queryVector.Length > 0)
            {
                options.VectorSearch = new VectorSearchOptions
                {
                    Queries =
            {
                new VectorizedQuery(queryVector)
                {
                    Fields = { "meaningVector" },
                    KNearestNeighborsCount = top
                }
            }
                };
            }

            var results = await _wordsClient.SearchAsync<WordSearchDocument>(
                query, options);

            var wordGroups = new Dictionary<string, List<string>>();

            await foreach (var result in results.Value.GetResultsAsync())
            {
                var doc = result.Document;
                if (!wordGroups.ContainsKey(doc.DocumentTitle))
                    wordGroups[doc.DocumentTitle] = new();

                wordGroups[doc.DocumentTitle].Add(
                    $"{doc.Word} ({doc.Pinyin}) — {doc.Meaning}");
            }

            if (!wordGroups.Any()) return string.Empty;

            var sb = new StringBuilder();
            foreach (var (docTitle, words) in wordGroups)
            {
                sb.AppendLine($"[From: {docTitle}]");
                sb.AppendLine(string.Join(", ", words));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ── Index document ─────────────────────────────────────────────────────

        public async Task IndexDocumentAsync(Models.Document document)
        {
            if (document.ExtractionStatus != "ready") return;

            _logger.LogInformation(
                "Indexing document {Id} into AI Search", document.Id);

            await Task.WhenAll(
                IndexWordsAsync(document),
                IndexChunksAsync(document)
            );

            _logger.LogInformation(
                "Indexing complete for document {Id}", document.Id);
        }

        private static string SanitizeKey(string key)
        {
            // Replace any character that's not letter, digit, _, -, or =
            return System.Text.RegularExpressions.Regex.Replace(key, @"[^a-zA-Z0-9_\-=]", "_");
        }

        private async Task IndexWordsAsync(Models.Document document)
        {
            if (!document.ExtractedWords.Any()) return;

            // Deduplicate words
            var uniqueWords = document.ExtractedWords
                .Where(w => !string.IsNullOrWhiteSpace(w.Word))
                .GroupBy(w => w.Word)
                .Select(g => g.OrderByDescending(w => w.Frequency).First())
                .ToList();

            // Generate embeddings for meanings in batch
            var meanings = uniqueWords.Select(w =>
                $"{w.Word} {w.Pinyin} {w.Meaning}").ToList();
            var embeddings = await _embeddings.GetEmbeddingsBatchAsync(meanings);

            var searchDocs = uniqueWords.Select((word, i) => new WordSearchDocument
            {
                Id = SanitizeKey($"{document.UserId}_{document.Id}_{word.Word}_{i}"),
                UserId = document.UserId,
                DocumentId = document.Id,
                Word = word.Word,
                Pinyin = word.Pinyin ?? string.Empty,
                Meaning = word.Meaning ?? string.Empty,
                HskLevel = word.HskLevel,
                Frequency = word.Frequency,
                DocumentTitle = document.FileName,
                DocumentType = document.FileType,
                IndexedAt = DateTimeOffset.UtcNow,
                MeaningVector = i < embeddings.Count && embeddings[i].Length > 0
                    ? embeddings[i] : null
            }).ToList();

            // Upload in batches of 100
            foreach (var batch in searchDocs.Chunk(100))
            {
                await _wordsClient.MergeOrUploadDocumentsAsync(batch);
            }

            _logger.LogInformation(
                "Indexed {Count} words for document {Id}",
                searchDocs.Count, document.Id);
        }

        private async Task IndexChunksAsync(Models.Document document)
        {
            _logger.LogInformation("[Chunks] ExtractedText length: {Len}", document.ExtractedText?.Length ?? 0);

            if (string.IsNullOrWhiteSpace(document.ExtractedText))
            {
                _logger.LogWarning("[Chunks] No extracted text for document {Id} — skipping chunks",
                    document.Id);
                return;
            }

            var chunks = ChunkText(document.ExtractedText, ChunkSize, ChunkOverlap);
            var embeddings = await _embeddings.GetEmbeddingsBatchAsync(chunks);

            var searchDocs = chunks.Select((chunk, i) => new ChunkSearchDocument
            {
                Id = SanitizeKey($"{document.UserId}_{document.Id}_chunk_{i}"),
                UserId = document.UserId,
                DocumentId = document.Id,
                DocumentTitle = document.FileName,
                DocumentType = document.FileType,
                ChunkIndex = i,
                Content = chunk,
                HskLevel = document.HskLevel,
                IndexedAt = DateTimeOffset.UtcNow,
                ContentVector = i < embeddings.Count && embeddings[i].Length > 0
                    ? embeddings[i] : null
            }).ToList();

            foreach (var batch in searchDocs.Chunk(100))
            {
                await _chunksClient.MergeOrUploadDocumentsAsync(batch);
            }

            _logger.LogInformation(
                "Indexed {Count} chunks for document {Id}",
                chunks.Count, document.Id);
        }

        // ── Delete from index ──────────────────────────────────────────────────

        public async Task DeleteDocumentFromIndexAsync(
            string documentId, string userId)
        {
            // Delete words
            var wordQuery = new SearchOptions
            {
                Filter = $"documentId eq '{documentId}' and userId eq '{userId}'",
                Select = { "id" },
                Size = 1000
            };
            var wordResults = await _wordsClient.SearchAsync<WordSearchDocument>(
                "*", wordQuery);
            var wordIds = new List<string>();
            await foreach (var result in wordResults.Value.GetResultsAsync())
                wordIds.Add(result.Document.Id);
            if (wordIds.Any())
                await _wordsClient.DeleteDocumentsAsync("id", wordIds);

            // Delete chunks
            var chunkQuery = new SearchOptions
            {
                Filter = $"documentId eq '{documentId}' and userId eq '{userId}'",
                Select = { "id" },
                Size = 1000
            };
            var chunkResults = await _chunksClient.SearchAsync<ChunkSearchDocument>(
                "*", chunkQuery);
            var chunkIds = new List<string>();
            await foreach (var result in chunkResults.Value.GetResultsAsync())
                chunkIds.Add(result.Document.Id);
            if (chunkIds.Any())
                await _chunksClient.DeleteDocumentsAsync("id", chunkIds);

            _logger.LogInformation(
                "Deleted index entries for document {Id}", documentId);
        }

        // ── Library search ─────────────────────────────────────────────────────

        public async Task<DocumentSearchResult> SearchDocumentsAsync(
            string userId, string query, int top = 10,
            string? fileTypeFilter = null, int? hskLevelFilter = null)
        {
            // Generate query embedding for vector search
            var queryVector = await _embeddings.GetEmbeddingAsync(query);

            var filter = $"userId eq '{userId}'";
            if (!string.IsNullOrEmpty(fileTypeFilter))
                filter += $" and documentType eq '{fileTypeFilter}'";
            if (hskLevelFilter.HasValue)
                filter += $" and hskLevel eq {hskLevelFilter}";

            var options = new SearchOptions
            {
                Filter = filter,
                Size = top * 3, // get more to group by document
                Select = {
                "id", "documentId", "documentTitle", "documentType",
                "word", "pinyin", "meaning", "hskLevel", "frequency"
            },
                QueryType = SearchQueryType.Semantic,
                SemanticSearch = new SemanticSearchOptions
                {
                    SemanticConfigurationName = "semantic-config",
                    SemanticQuery = query,
                    QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
                }
            };

            // Add vector search if embedding succeeded
            if (queryVector.Length > 0)
            {
                options.VectorSearch = new VectorSearchOptions
                {
                    Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        Fields = { "meaningVector" },
                        KNearestNeighborsCount = top * 2
                    }
                }
                };
            }

            var results = await _wordsClient.SearchAsync<WordSearchDocument>(
                query, options);

            // Group results by document
            var documentGroups = new Dictionary<string, DocumentSearchHit>();

            await foreach (var result in results.Value.GetResultsAsync())
            {
                var doc = result.Document;
                if (!documentGroups.ContainsKey(doc.DocumentId))
                {
                    documentGroups[doc.DocumentId] = new DocumentSearchHit
                    {
                        DocumentId = doc.DocumentId,
                        DocumentTitle = doc.DocumentTitle,
                        DocumentType = doc.DocumentType,
                        HskLevel = doc.HskLevel,
                        Score = result.Score ?? 0,
                        TopWords = new List<WordHit>()
                    };
                }

                var hit = documentGroups[doc.DocumentId];
                if (hit.TopWords.Count < 5)
                {
                    hit.TopWords.Add(new WordHit
                    {
                        Word = doc.Word,
                        Pinyin = doc.Pinyin,
                        Meaning = doc.Meaning,
                        HskLevel = doc.HskLevel
                    });
                }
                hit.Score = Math.Max(hit.Score, result.Score ?? 0);
            }

            return new DocumentSearchResult
            {
                Hits = documentGroups.Values
                    .OrderByDescending(h => h.Score)
                    .Take(top)
                    .ToList(),
                TotalCount = documentGroups.Count,
                Query = query
            };
        }

        // ── RAG chunk search ───────────────────────────────────────────────────

        public async Task<RagResult> SearchChunksForRagAsync(
            string userId, string query, int topChunks = 5, string? documentTitleHint = null)
        {
            var queryVector = await _embeddings.GetEmbeddingAsync(query);

            // If a specific document is mentioned, filter to it
            var filter = $"userId eq '{userId}'";
            if (!string.IsNullOrEmpty(documentTitleHint))
            {
                // Strip extension for partial match
                var titleWithoutExt = Path.GetFileNameWithoutExtension(documentTitleHint);
                filter += $" and search.ismatch('{titleWithoutExt}', 'documentTitle')";
            }

            var options = new SearchOptions
            {
                Filter = filter,
                Size = topChunks,
                Select = { "documentId", "documentTitle", "content", "chunkIndex" },
                QueryType = SearchQueryType.Semantic,
                SemanticSearch = new SemanticSearchOptions
                {
                    SemanticConfigurationName = "semantic-config",
                    QueryCaption = new QueryCaption(QueryCaptionType.Extractive)
                }
            };

            if (queryVector.Length > 0)
            {
                options.VectorSearch = new VectorSearchOptions
                {
                    Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        Fields = { "contentVector" },
                        KNearestNeighborsCount = topChunks
                    }
                }
                };
            }

            var results = await _chunksClient.SearchAsync<ChunkSearchDocument>(
                query, options);

            var chunks = new List<RagChunk>();
            await foreach (var result in results.Value.GetResultsAsync())
            {
                chunks.Add(new RagChunk
                {
                    DocumentId = result.Document.DocumentId,
                    DocumentTitle = result.Document.DocumentTitle,
                    Content = result.Document.Content,
                    Score = result.Score ?? 0
                });
            }

            // Build combined context for GPT
            var context = string.Join("\n\n---\n\n",
                chunks.Select((c, i) =>
                    $"[Source {i + 1}: {c.DocumentTitle}]\n{c.Content}"));

            return new RagResult
            {
                Chunks = chunks,
                CombinedContext = context
            };
        }

        // ── Text chunking ──────────────────────────────────────────────────────

        private static List<string> ChunkText(
            string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return chunks;

            // Split on sentence boundaries where possible
            var sentences = text.Split(
                new[] { '。', '！', '？', '.', '!', '?' },
                StringSplitOptions.RemoveEmptyEntries);

            var current = new System.Text.StringBuilder();

            foreach (var sentence in sentences)
            {
                if (current.Length + sentence.Length > chunkSize &&
                    current.Length > 0)
                {
                    chunks.Add(current.ToString().Trim());
                    // Keep overlap from end of previous chunk
                    var words = current.ToString().Split(' ');
                    var overlapText = string.Join(" ",
                        words.TakeLast(overlap / 10));
                    current.Clear();
                    current.Append(overlapText + " ");
                }
                current.Append(sentence + "。");
            }

            if (current.Length > 0)
                chunks.Add(current.ToString().Trim());

            return chunks;
        }
    }
}
