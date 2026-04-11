using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Users;

namespace BambooBrain_Service.Services.Settings
{
    public class SettingsService : ISettingsService
    {
        private readonly IUserRepository _users;
        private readonly IConfiguration _config;
        private readonly ILogger<SettingsService> _logger;

        public SettingsService(
            IUserRepository users,
            IConfiguration config,
            ILogger<SettingsService> logger)
        {
            _users = users;
            _config = config;
            _logger = logger;
        }

        public async Task<User> GetUserAsync(string userId)
        {
            var user = await _users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");
            return user;
        }

        // ── Profile update ─────────────────────────────────────────────────────

        public async Task<User> UpdateProfileAsync(
            string userId, UpdateProfileRequest request)
        {
            var user = await _users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("Name cannot be empty.");

            user.Name = request.Name.Trim();
            user.Bio = request.Bio.Trim();

            return await _users.UpdateAsync(user);
        }

        // ── Avatar upload ──────────────────────────────────────────────────────

        public async Task<User> UpdateAvatarAsync(string userId, IFormFile file)
        {
            var user = await _users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            // Validate image type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType))
                throw new InvalidOperationException(
                    "Only JPEG, PNG or WebP images are allowed.");

            if (file.Length > 5_242_880) // 5MB
                throw new InvalidOperationException(
                    "Avatar image must be under 5MB.");

            var blobServiceClient = new BlobServiceClient(
                _config["BlobStorage:ConnectionString"]!);
            var containerClient = blobServiceClient.GetBlobContainerClient("avatars");

            // Delete old avatar if exists
            if (!string.IsNullOrEmpty(user.Image))
            {
                try
                {
                    var oldBlobName = $"{userId}/avatar{Path.GetExtension(user.Image.Split('?')[0])}";
                    await containerClient.GetBlobClient(oldBlobName).DeleteIfExistsAsync();
                }
                catch { /* ignore delete errors */ }
            }

            // Upload new avatar
            var ext = file.ContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
            var blobName = $"{userId}/avatar{ext}";
            var blobClient = containerClient.GetBlobClient(blobName);

            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, overwrite: true);
            // Then set content type separately:
            await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders
            {
                ContentType = file.ContentType
            });

            // Generate long-lived SAS URL (1 year)
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "avatars",
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddYears(1)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);
            var avatarUrl = blobClient.GenerateSasUri(sasBuilder).ToString();

            user.Image = avatarUrl;
            return await _users.UpdateAsync(user);
        }

        // ── Email update ───────────────────────────────────────────────────────

        public async Task<User> UpdateEmailAsync(
            string userId, UpdateEmailRequest request)
        {
            var user = await _users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            // Only credentials users can change email
            if (user.Provider != "credentials")
                throw new InvalidOperationException(
                    "Email cannot be changed for social login accounts.");

            // Verify current password
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                throw new InvalidOperationException("Current password is incorrect.");

            // Check new email not already taken
            var existing = await _users.GetByEmailAsync(request.NewEmail);
            if (existing != null && existing.Id != userId)
                throw new InvalidOperationException(
                    "This email is already in use.");

            user.Email = request.NewEmail.ToLower().Trim();
            return await _users.UpdateAsync(user);
        }

        // ── Password update ────────────────────────────────────────────────────

        public async Task<User> UpdatePasswordAsync(
            string userId, UpdatePasswordRequest request)
        {
            var user = await _users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            if (user.Provider != "credentials")
                throw new InvalidOperationException(
                    "Password cannot be changed for social login accounts.");

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                throw new InvalidOperationException(
                    "Current password is incorrect.");

            if (request.NewPassword.Length < 8)
                throw new InvalidOperationException(
                    "New password must be at least 8 characters.");

            if (request.CurrentPassword == request.NewPassword)
                throw new InvalidOperationException(
                    "New password must be different from current password.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                request.NewPassword, workFactor: 12);
            user.PasswordChangedAt = DateTime.UtcNow;

            return await _users.UpdateAsync(user);
        }

        // ── Integrations ───────────────────────────────────────────────────────

        public async Task<User> UpdateIntegrationsAsync(
            string userId, UpdateIntegrationRequest request)
        {
            var user = await _users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            user.IsGcalSyncEnabled = request.IsGcalSyncEnabled;
            user.IsMicrosoftAccountEnabled = request.IsMicrosoftAccountEnabled;

            return await _users.UpdateAsync(user);
        }
    }

}
