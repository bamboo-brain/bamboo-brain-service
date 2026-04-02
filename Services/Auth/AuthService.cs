using BambooBrain_Service.Helpers;
using BambooBrain_Service.Repositories.Users;
using BambooBrain_Service.Models;
using BCrypt.Net;

namespace BambooBrain_Service.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _users;
        private readonly JwtHelper _jwt;

        public AuthService(IUserRepository users, JwtHelper jwt)
        {
            _users = users;
            _jwt = jwt;
        }

        public async Task<(bool, string, string?, User?)> RegisterAsync(RegisterRequest request)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.Name))
                return (false, "All fields are required.", null, null);

            if (request.Password.Length < 8)
                return (false, "Password must be at least 8 characters.", null, null);

            // Check if email already exists
            var existing = await _users.GetByEmailAsync(request.Email);
            if (existing != null)
                return (false, "An account with this email already exists.", null, null);

            // Hash password and create user
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
                Provider = "credentials"
            };

            var created = await _users.CreateAsync(user);
            var token = _jwt.GenerateToken(created);

            return (true, "Account created successfully.", token, created);
        }

        public async Task<(bool, string, string?, User?)> LoginAsync(LoginRequest request)
        {
            var user = await _users.GetByEmailAsync(request.Email);

            if (user == null || user.Provider != "credentials")
                return (false, "Invalid email or password.", null, null);

            var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!passwordValid)
                return (false, "Invalid email or password.", null, null);

            var token = _jwt.GenerateToken(user);
            return (true, "Login successful.", token, user);
        }
    }
}
