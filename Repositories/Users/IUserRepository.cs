using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Users
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User> CreateAsync(User user);
        Task<User?> GetByIdAsync(string id);
        Task<User> UpdateAsync(User user);
        Task<User> UpsertOAuthUserAsync(UpsertOAuthUserRequest request);
    }
}
