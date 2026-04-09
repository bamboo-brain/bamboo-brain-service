using BambooBrain_Service.Models;
using BambooBrain_Service.Services.Flashcard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/flashcards")]
    [Authorize]
    public class FlashcardsController : ControllerBase
    {
        private readonly IFlashcardService _flashcards;

        public FlashcardsController(IFlashcardService flashcards)
        {
            _flashcards = flashcards;
        }

        // Get all decks
        [HttpGet("decks")]
        public async Task<IActionResult> GetDecks()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var decks = await _flashcards.GetUserDecksAsync(userId);
            return Ok(decks);
        }

        // Get single deck
        [HttpGet("decks/{id}")]
        public async Task<IActionResult> GetDeck(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var deck = await _flashcards.GetDeckAsync(id, userId);
            if (deck == null) return NotFound();

            return Ok(deck);
        }

        // Create deck manually
        [HttpPost("decks")]
        public async Task<IActionResult> CreateDeck([FromBody] CreateDeckRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Deck name is required." });

            var deck = await _flashcards.CreateDeckAsync(userId, request);
            return Ok(deck);
        }

        // Auto-create deck from document
        [HttpPost("decks/from-document")]
        public async Task<IActionResult> CreateFromDocument(
            [FromBody] CreateDeckFromDocumentRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.DocumentId))
                return BadRequest(new { message = "DocumentId is required." });

            try
            {
                var deck = await _flashcards.CreateDeckFromDocumentAsync(userId, request);
                return Ok(deck);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Add card to deck
        [HttpPost("decks/{id}/cards")]
        public async Task<IActionResult> AddCard(
            string id, [FromBody] AddCardRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Word))
                return BadRequest(new { message = "Word is required." });

            try
            {
                var deck = await _flashcards.AddCardAsync(id, userId, request);
                return Ok(deck);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Remove card from deck
        [HttpDelete("decks/{deckId}/cards/{cardId}")]
        public async Task<IActionResult> RemoveCard(string deckId, string cardId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var deck = await _flashcards.RemoveCardAsync(deckId, cardId, userId);
                return Ok(deck);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Get due cards for today
        [HttpGet("decks/{id}/due")]
        public async Task<IActionResult> GetDueCards(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var cards = await _flashcards.GetDueCardsAsync(id, userId);
                return Ok(new
                {
                    cards,
                    count = cards.Count,
                    deckId = id
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Review a card (SM-2)
        [HttpPost("decks/{id}/review")]
        public async Task<IActionResult> ReviewCard(
            string id, [FromBody] ReviewCardRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (request.Grade < 0 || request.Grade > 5)
                return BadRequest(new { message = "Grade must be between 0 and 5." });

            try
            {
                var deck = await _flashcards.ReviewCardAsync(id, userId, request);

                // Return updated card for the frontend
                var updatedCard = deck.Cards.FirstOrDefault(c => c.Id == request.CardId);
                return Ok(new
                {
                    card = updatedCard,
                    nextReviewDate = updatedCard?.NextReviewDate,
                    intervalDays = updatedCard?.IntervalDays,
                    easeFactor = updatedCard?.EaseFactor
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Delete deck
        [HttpDelete("decks/{id}")]
        public async Task<IActionResult> DeleteDeck(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            await _flashcards.DeleteDeckAsync(id, userId);
            return NoContent();
        }

        // Get study stats across all decks
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var decks = await _flashcards.GetUserDecksAsync(userId);

            var stats = new
            {
                totalDecks = decks.Count,
                totalCards = decks.Sum(d => d.Cards.Count),
                totalDueToday = decks.Sum(d => d.DueToday),
                averageMastery = decks.Any()
                    ? Math.Round(decks.Average(d => d.MasteryPercentage), 1)
                    : 0,
                deckStats = decks.Select(d => new
                {
                    deckId = d.Id,
                    name = d.Name,
                    totalCards = d.Cards.Count,
                    dueToday = d.DueToday,
                    mastery = d.MasteryPercentage,
                    lastStudied = d.Cards
                        .Where(c => c.LastReviewDate.HasValue)
                        .OrderByDescending(c => c.LastReviewDate)
                        .FirstOrDefault()?.LastReviewDate
                })
            };

            return Ok(stats);
        }
    }

}
