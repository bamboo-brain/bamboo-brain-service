namespace BambooBrain_Service.Services.BlobStorage
{
    public interface IBlobStorageService
    {
        Task<(string blobPath, string blobUrl)> UploadAsync(
            Stream fileStream, string fileName, string contentType, string containerName);
        Task DeleteAsync(string blobPath, string containerName);
        Task<Stream> DownloadAsync(string blobPath, string containerName);
        Task<string> GenerateSasUrlAsync(string blobPath, string containerName, int expiryMinutes = 60);
    }
}
