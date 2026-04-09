using BambooBrain_Service.Models;

namespace BambooBrain_Service.Services.Flashcard
{
    public interface IFlashcardService
    {
        Task<FlashcardDeck> CreateDeckAsync(string userId, CreateDeckRequest request);
        Task<FlashcardDeck> CreateDeckFromDocumentAsync(string userId, CreateDeckFromDocumentRequest request);
        Task<List<FlashcardDeck>> GetUserDecksAsync(string userId);
        Task<FlashcardDeck?> GetDeckAsync(string id, string userId);
        Task<FlashcardDeck> AddCardAsync(string deckId, string userId, AddCardRequest request);
        Task<FlashcardDeck> RemoveCardAsync(string deckId, string cardId, string userId);
        Task<FlashcardDeck> ReviewCardAsync(string deckId, string userId, ReviewCardRequest request);
        Task<List<Models.Flashcard>> GetDueCardsAsync(string deckId, string userId);
        Task DeleteDeckAsync(string id, string userId);
    }
}
