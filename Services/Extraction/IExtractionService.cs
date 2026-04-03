using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Extraction
{
    public interface IExtractionService
    {
        Task ExtractAsync(Models.Document document);
    }
}
