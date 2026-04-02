using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/onboarding")]
    [Authorize]  // requires valid JWT
    public class OnboardingController : ControllerBase
    {
        private readonly IUserRepository _users;

        public OnboardingController(IUserRepository users)
        {
            _users = users;
        }

        [HttpPost("complete")]
        public async Task<IActionResult> CompleteOnboarding([FromBody] OnboardingRequest request)
        {
            // Get user ID from JWT claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _users.GetByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });

            // Update onboarding fields
            user.HskLevel = request.HskLevel;
            user.IsGcalSyncEnabled = request.IsGcalSyncEnabled;
            user.IsMicrosoftAccountEnabled = request.IsMicrosoftAccountEnabled;
            user.AreaOfInterests = request.AreaOfInterests;
            user.IsOnboardingComplete = true;

            await _users.UpdateAsync(user);

            return Ok(new
            {
                message = "Onboarding complete.",
                user = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.HskLevel,
                    user.IsGcalSyncEnabled,
                    user.IsMicrosoftAccountEnabled,
                    user.AreaOfInterests,
                    user.IsOnboardingComplete
                }
            });
        }

        [HttpPost("skip")]
        public async Task<IActionResult> SkipOnboarding()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _users.GetByIdAsync(userId);
            if (user == null) return NotFound();

            // Do NOT set IsOnboardingComplete = true
            // Just return — user stays in pending onboarding state
            return Ok(new { message = "Onboarding skipped. Will be prompted again on next login." });
        }
    }
}
