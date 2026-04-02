using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;

namespace BambooBrain_Service.Services.BlobStorage
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;

        public BlobStorageService(IConfiguration config)
        {
            _blobServiceClient = new BlobServiceClient(
                config["BlobStorage:ConnectionString"]);
        }

        public async Task<(string blobPath, string blobUrl)> UploadAsync(
            Stream fileStream, string fileName, string contentType, string containerName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            // Create unique blob path with userId folder structure
            var blobPath = $"{Guid.NewGuid()}/{fileName}";
            var blobClient = containerClient.GetBlobClient(blobPath);

            await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
            {
                ContentType = contentType
            });

            return (blobPath, blobClient.Uri.ToString());
        }

        public async Task DeleteAsync(string blobPath, string containerName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);
            await blobClient.DeleteIfExistsAsync();
        }

        public async Task<Stream> DownloadAsync(string blobPath, string containerName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);
            var response = await blobClient.DownloadAsync();
            return response.Value.Content;
        }
    }
}
