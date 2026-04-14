namespace BambooBrain_Service.Services.Search
{
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text);
        Task<List<float[]>> GetEmbeddingsBatchAsync(List<string> texts);
    }
}
