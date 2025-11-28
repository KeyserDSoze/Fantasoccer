using Fantasoccer.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Application.Api.Controllers
{
    [Authorize(Policy = "authenticated")]
    [ApiController]
    [Route("[controller]/[action]")]
    public class CardController : ControllerBase
    {
        private readonly IRepository<CardWrapper, string> _cardRepository;
        private readonly IFactory<IContentRepository> _contentRepositoryFactory;

        public CardController(
            IRepository<CardWrapper, string> cardRepository,
            IFactory<IContentRepository> contentRepositoryFactory)
        {
            _cardRepository = cardRepository;
            _contentRepositoryFactory = contentRepositoryFactory;
        }

        /// <summary>
        /// Get all cards - available to all authenticated users
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "authenticated")]
        public async Task<ActionResult<IEnumerable<Card>>> GetAllCardsAsync()
        {
            try
            {
                var cardWrapper = await _cardRepository.GetAsync("default");
                if (cardWrapper?.Cards == null || cardWrapper.Cards.Count == 0)
                    return Ok(new List<Card>());

                return Ok(cardWrapper.Cards);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve cards", details = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific card by ID - available to all authenticated users
        /// </summary>
        [HttpGet("{cardId}")]
        [Authorize(Policy = "authenticated")]
        public async Task<ActionResult<Card>> GetCardByIdAsync([FromRoute] string cardId)
        {
            try
            {
                var cardWrapper = await _cardRepository.GetAsync("default");
                if (cardWrapper?.Cards == null)
                    return NotFound(new { error = "No cards found" });

                var card = cardWrapper.Cards.FirstOrDefault(c => c.Id == cardId);
                if (card == null)
                    return NotFound(new { error = $"Card with id '{cardId}' not found" });

                return Ok(card);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve card", details = ex.Message });
            }
        }

        /// <summary>
        /// Create a new card - SuperAdmin only
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "super_admin")]
        public async Task<ActionResult<Card>> CreateCardAsync([FromBody] Card request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description))
                    return BadRequest(new { error = "Name and Description are required" });

                var cardWrapper = await _cardRepository.GetAsync("default");
                if (cardWrapper == null)
                {
                    cardWrapper = new CardWrapper { Cards = [] };
                }

                var newCard = new Card
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = request.Name,
                    Description = request.Description,
                    Type = request.Type,
                    Points = request.Points,
                    EnemyPoints = request.EnemyPoints,
                    MyCondition = request.MyCondition,
                    EnemyCondition = request.EnemyCondition
                };

                cardWrapper.Cards.Add(newCard);
                await _cardRepository.UpdateAsync("default", cardWrapper);

                return CreatedAtAction(nameof(GetCardByIdAsync), new { cardId = newCard.Id }, newCard);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create card", details = ex.Message });
            }
        }

        /// <summary>
        /// Upload card image - SuperAdmin only
        /// Image will be saved in the cardimages container with the card ID as filename
        /// </summary>
        [HttpPost("image/{cardId}")]
        [Authorize(Policy = "super_admin")]
        [RequestSizeLimit(5_242_880)] // 5 MB
        public async Task<ActionResult> UploadCardImageAsync([FromRoute] string cardId, IFormFile file)
        {
            try
            {
                // Validate card exists
                var cardWrapper = await _cardRepository.GetAsync("default");
                if (cardWrapper?.Cards == null || !cardWrapper.Cards.Any(c => c.Id == cardId))
                    return NotFound(new { error = $"Card with id '{cardId}' not found" });

                // Validate file
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No file provided" });

                const long maxFileSize = 5 * 1024 * 1024; // 5 MB
                if (file.Length > maxFileSize)
                    return BadRequest(new { error = "File size exceeds 5 MB limit" });

                var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                if (!allowedMimeTypes.Contains(file.ContentType?.ToLower()))
                    return BadRequest(new { error = "Invalid file type. Allowed: jpeg, png, gif, webp" });

                // Upload image using ContentRepository
                byte[] fileBytes;
                using (var stream = file.OpenReadStream())
                using (var memoryStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                var contentRepository = _contentRepositoryFactory.Create("cards");
                var success = await contentRepository.UploadAsync(cardId, fileBytes);

                if (!success)
                    return StatusCode(500, new { error = "Failed to upload image" });

                return Ok(new { cardId = cardId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to upload card image", details = ex.Message });
            }
        }

        /// <summary>
        /// Update a card - SuperAdmin only
        /// </summary>
        [HttpPut("{cardId}")]
        [Authorize(Policy = "super_admin")]
        public async Task<ActionResult<Card>> UpdateCardAsync([FromRoute] string cardId, [FromBody] Card request)
        {
            try
            {
                var cardWrapper = await _cardRepository.GetAsync("default");
                if (cardWrapper?.Cards == null)
                    return NotFound(new { error = "No cards found" });

                var card = cardWrapper.Cards.FirstOrDefault(c => c.Id == cardId);
                if (card == null)
                    return NotFound(new { error = $"Card with id '{cardId}' not found" });

                if (!string.IsNullOrWhiteSpace(request.Name))
                    card.Name = request.Name;
                if (!string.IsNullOrWhiteSpace(request.Description))
                    card.Description = request.Description;
                card.Type = request.Type;
                card.Points = request.Points;
                card.EnemyPoints = request.EnemyPoints;
                if (request.MyCondition != null)
                    card.MyCondition = request.MyCondition;
                if (request.EnemyCondition != null)
                    card.EnemyCondition = request.EnemyCondition;

                await _cardRepository.UpdateAsync("default", cardWrapper);
                return Ok(card);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update card", details = ex.Message });
            }
        }

        /// <summary>
        /// Delete a card - SuperAdmin only
        /// </summary>
        [HttpDelete("{cardId}")]
        [Authorize(Policy = "super_admin")]
        public async Task<ActionResult> DeleteCardAsync([FromRoute] string cardId)
        {
            try
            {
                var cardWrapper = await _cardRepository.GetAsync("default");
                if (cardWrapper?.Cards == null)
                    return NotFound(new { error = "No cards found" });

                var card = cardWrapper.Cards.FirstOrDefault(c => c.Id == cardId);
                if (card == null)
                    return NotFound(new { error = $"Card with id '{cardId}' not found" });

                cardWrapper.Cards.Remove(card);
                await _cardRepository.UpdateAsync("default", cardWrapper);

                // Delete associated image from ContentRepository
                try
                {
                    var contentRepository = _contentRepositoryFactory.Create("cards");
                    await contentRepository.DeleteAsync(cardId);
                }
                catch
                {
                    // Image might not exist, continue with card deletion
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to delete card", details = ex.Message });
            }
        }
    }
}
