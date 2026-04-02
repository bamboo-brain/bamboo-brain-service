using BambooBrain_Service.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using BambooBrain_Service.Models;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var (success, message, token, user) = await _authService.RegisterAsync(request);

            if (!success)
                return BadRequest(new { message });

            return Ok(new
            {
                message,
                token,
                user = new { user!.Id, user.Name, user.Email, user.HskLevel }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var (success, message, token, user) = await _authService.LoginAsync(request);

            if (!success)
                return Unauthorized(new { message });

            return Ok(new
            {
                message,
                token,
                user = new { user!.Id, user.Name, user.Email, user.HskLevel }
            });
        }
    }

}
