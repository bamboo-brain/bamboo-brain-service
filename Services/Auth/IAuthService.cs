using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Auth
{
    public interface IAuthService
    {
        Task<(bool success, string message, string? token, User? user)> RegisterAsync(RegisterRequest request);
        Task<(bool success, string message, string? token, User? user)> LoginAsync(LoginRequest request);
    }
}
