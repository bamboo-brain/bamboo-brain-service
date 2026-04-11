using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Settings
{
    public interface ISettingsService
    {
        Task<User> UpdateProfileAsync(string userId, UpdateProfileRequest request);
        Task<User> UpdateAvatarAsync(string userId, IFormFile file);
        Task<User> UpdateEmailAsync(string userId, UpdateEmailRequest request);
        Task<User> UpdatePasswordAsync(string userId, UpdatePasswordRequest request);
        Task<User> UpdateIntegrationsAsync(string userId, UpdateIntegrationRequest request);
        Task<User> GetUserAsync(string userId);
    }
}
