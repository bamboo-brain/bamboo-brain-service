using BambooBrain_Service.Models;

namespace BambooBrain_Service.Repositories.Flashcards
{
    public interface IFlashcardRepository
    {
        Task<FlashcardDeck> CreateAsync(FlashcardDeck deck);
        Task<FlashcardDeck?> GetByIdAsync(string id, string userId);
        Task<List<FlashcardDeck>> GetByUserIdAsync(string userId);
        Task<FlashcardDeck> UpdateAsync(FlashcardDeck deck);
        Task DeleteAsync(string id, string userId);
    }
}
