using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Indexes;
using Azure;

namespace BambooBrain_Service.Services.Search
{
    public class SearchIndexSetup
    {
        private readonly SearchIndexClient _indexClient;
        private readonly IConfiguration _config;
        private readonly ILogger<SearchIndexSetup> _logger;

        public SearchIndexSetup(IConfiguration config, ILogger<SearchIndexSetup> logger)
        {
            _config = config;
            _logger = logger;
            _indexClient = new SearchIndexClient(
                new Uri(config["AzureSearch:Endpoint"]!),
                new AzureKeyCredential(config["AzureSearch:AdminKey"]!)
            );
        }

        public async Task EnsureIndexesExistAsync()
        {
            await EnsureWordsIndexAsync();
            await EnsureChunksIndexAsync();
        }

        // ── Words index ────────────────────────────────────────────────────────

        private async Task EnsureWordsIndexAsync()
        {
            var indexName = _config["AzureSearch:WordsIndex"]!;

            var fields = new List<SearchField>
        {
            new SimpleField("id", SearchFieldDataType.String)
                { IsKey = true, IsFilterable = true },
            new SimpleField("userId", SearchFieldDataType.String)
                { IsFilterable = true },
            new SimpleField("documentId", SearchFieldDataType.String)
                { IsFilterable = true },
            new SearchableField("word") { IsFilterable = true },
            new SearchableField("pinyin") { IsFilterable = true },
            new SearchableField("meaning") { IsFilterable = true },
            new SimpleField("hskLevel", SearchFieldDataType.Int32)
                { IsFilterable = true, IsSortable = true },
            new SimpleField("frequency", SearchFieldDataType.Int32)
                { IsFilterable = true, IsSortable = true },
            new SimpleField("documentTitle", SearchFieldDataType.String)
                { IsFilterable = true },
            new SimpleField("documentType", SearchFieldDataType.String)
                { IsFilterable = true },
            new SimpleField("indexedAt", SearchFieldDataType.DateTimeOffset)
                { IsFilterable = true, IsSortable = true },
            // Vector field for semantic similarity
            new VectorSearchField("meaningVector", 1536,
                "hnsw-config")
        };

            var vectorSearch = new VectorSearch();
            vectorSearch.Algorithms.Add(
                new HnswAlgorithmConfiguration("hnsw-config"));

            var index = new SearchIndex(indexName, fields)
            {
                VectorSearch = vectorSearch,
                SemanticSearch = new SemanticSearch
                {
                    Configurations =
                {
                    new SemanticConfiguration("semantic-config",
                        new SemanticPrioritizedFields
                        {
                            TitleField = new SemanticField("word"),
                            ContentFields =
                            {
                                new SemanticField("meaning"),
                                new SemanticField("pinyin")
                            }
                        })
                }
                }
            };

            try
            {
                await _indexClient.CreateOrUpdateIndexAsync(index);
                _logger.LogInformation("Words index ensured: {Index}", indexName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create words index");
            }
        }

        // ── Document chunks index ──────────────────────────────────────────────

        private async Task EnsureChunksIndexAsync()
        {
            var indexName = _config["AzureSearch:ChunksIndex"]!;

            var fields = new List<SearchField>
        {
            new SimpleField("id", SearchFieldDataType.String)
                { IsKey = true, IsFilterable = true },
            new SimpleField("userId", SearchFieldDataType.String)
                { IsFilterable = true },
            new SimpleField("documentId", SearchFieldDataType.String)
                { IsFilterable = true },
            new SimpleField("documentTitle", SearchFieldDataType.String)
                { IsFilterable = true },
            new SimpleField("documentType", SearchFieldDataType.String)
                { IsFilterable = true },
            new SimpleField("chunkIndex", SearchFieldDataType.Int32)
                { IsFilterable = true, IsSortable = true },
            new SearchableField("content") { IsFilterable = false },
            new SimpleField("hskLevel", SearchFieldDataType.Int32)
                { IsFilterable = true },
            new SimpleField("indexedAt", SearchFieldDataType.DateTimeOffset)
                { IsFilterable = true, IsSortable = true },
            // Vector field for semantic similarity
            new VectorSearchField("contentVector", 1536, "hnsw-config")
        };

            var vectorSearch = new VectorSearch();
            vectorSearch.Algorithms.Add(
                new HnswAlgorithmConfiguration("hnsw-config"));

            var index = new SearchIndex(indexName, fields)
            {
                VectorSearch = vectorSearch,
                SemanticSearch = new SemanticSearch
                {
                    Configurations =
                {
                    new SemanticConfiguration("semantic-config",
                        new SemanticPrioritizedFields
                        {
                            ContentFields =
                            {
                                new SemanticField("content"),
                                new SemanticField("documentTitle")
                            }
                        })
                }
                }
            };

            try
            {
                await _indexClient.CreateOrUpdateIndexAsync(index);
                _logger.LogInformation("Chunks index ensured: {Index}", indexName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create chunks index");
            }
        }
    }
}
