using Azure.Core;
using BambooBrain_Service.Models;
using BambooBrain_Service.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Security.Claims;
using BambooBrain_Service.Attributes;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/settings")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsService _settings;

        public SettingsController(ISettingsService settings)
        {
            _settings = settings;
        }

        // Get current user profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _settings.GetUserAsync(userId);
            return Ok(new
            {
                user.Id,
                user.Name,
                user.Email,
                user.Bio,
                user.Image,
                user.Provider,
                user.HskLevel,
                user.IsGcalSyncEnabled,
                user.IsMicrosoftAccountEnabled,
                user.AreaOfInterests,
                user.CreatedAt,
                passwordChangedAt = user.PasswordChangedAt,
                isCredentialsUser = user.Provider == "credentials"
            });
        }

        // Update profile (name + bio)
        [HttpPatch("profile")]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var user = await _settings.UpdateProfileAsync(userId, request);
                return Ok(new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.Bio,
                    user.Image,
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Upload avatar
        [HttpPost("avatar")]
        [DisableFormValueModelBinding]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadAvatar()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (!Request.ContentType?.Contains("multipart/") ?? true)
                return BadRequest(new { message = "Must be multipart/form-data." });

            var boundary = HeaderUtilities.RemoveQuotes(
                MediaTypeHeaderValue.Parse(Request.ContentType).Boundary).Value;

            var reader = new MultipartReader(boundary!, Request.Body);
            MultipartSection? section;

            while ((section = await reader.ReadNextSectionAsync()) != null)
            {
                var hasContentDisposition = ContentDispositionHeaderValue.TryParse(
                    section.ContentDisposition, out var contentDisposition);

                if (!hasContentDisposition ||
                    !contentDisposition!.FileName.HasValue) continue;

                var memoryStream = new MemoryStream();
                await section.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var formFile = new FormFile(
                    memoryStream, 0, memoryStream.Length,
                    "file", contentDisposition.FileName.Value)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = section.ContentType ?? "image/jpeg"
                };

                try
                {
                    var user = await _settings.UpdateAvatarAsync(userId, formFile);
                    return Ok(new { avatarUrl = user.Image });
                }
                catch (InvalidOperationException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }
            }

            return BadRequest(new { message = "No file found in request." });
        }

        // Update email (credentials users only)
        [HttpPatch("email")]
        public async Task<IActionResult> UpdateEmail(
            [FromBody] UpdateEmailRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.NewEmail))
                return BadRequest(new { message = "New email is required." });

            try
            {
                var user = await _settings.UpdateEmailAsync(userId, request);
                return Ok(new { user.Email, message = "Email updated successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Update password (credentials users only)
        [HttpPatch("password")]
        public async Task<IActionResult> UpdatePassword(
            [FromBody] UpdatePasswordRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "New password is required." });

            try
            {
                await _settings.UpdatePasswordAsync(userId, request);
                return Ok(new { message = "Password updated successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Update integrations (Google Calendar, Microsoft)
        [HttpPatch("integrations")]
        public async Task<IActionResult> UpdateIntegrations(
            [FromBody] UpdateIntegrationRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _settings.UpdateIntegrationsAsync(userId, request);
            return Ok(new
            {
                user.IsGcalSyncEnabled,
                user.IsMicrosoftAccountEnabled,
                message = "Integrations updated."
            });
        }
    }
}
