using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Users
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User> CreateAsync(User user);
    }
}
