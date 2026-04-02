using BambooBrain_Service.Helpers;
using BambooBrain_Service.Models;
using BambooBrain_Service.Repositories.Users;
using Microsoft.AspNetCore.Mvc;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _users;
        private readonly JwtHelper _jwt;

        public UsersController(IUserRepository users, JwtHelper jwt)
        {
            _users = users;
            _jwt = jwt;
        }

        [HttpPost("upsert-oauth")]
        public async Task<IActionResult> UpsertOAuth([FromBody] UpsertOAuthUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required." });

            var user = await _users.UpsertOAuthUserAsync(request);

            // Generate JWT so frontend can make authorized requests
            var token = _jwt.GenerateToken(user);

            return Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.Image,
                    user.Provider,
                    user.HskLevel,
                    user.IsOnboardingComplete
                }
            });
        }
    }
}
