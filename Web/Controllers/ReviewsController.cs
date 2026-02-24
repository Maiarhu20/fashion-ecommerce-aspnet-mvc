using Core.DTOs.Reviews;
using Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    [Route("reviews")]
    public class ReviewsController : Controller
    {
        private readonly ReviewService _reviewService;
        private readonly ILogger<ReviewsController> _logger;

        public ReviewsController( ReviewService reviewService, ILogger<ReviewsController> logger)
        {
            _reviewService = reviewService;
            _logger = logger;
        }

        // GET: /reviews/product/{productId}
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductReviews(int productId)
        {
            try
            {
                var result = await _reviewService.GetProductReviewsAsync(productId);

                if (result.Succeeded)
                {
                    return PartialView("_ProductReviews", result.Data);
                }
                else
                {
                    _logger.LogWarning("Failed to get product reviews for product {ProductId}: {Error}",
                        productId, result.ErrorMessage);

                    return PartialView("_ProductReviews", new ProductReviewsDto
                    {
                        ProductId = productId,
                        ProductName = "Unknown Product",
                        Reviews = new List<ReviewResponseDto>(),
                        AverageRating = 0,
                        TotalReviews = 0
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product reviews for product {ProductId}", productId);
                return PartialView("_ProductReviews", new ProductReviewsDto
                {
                    ProductId = productId,
                    ProductName = "Unknown Product",
                    Reviews = new List<ReviewResponseDto>(),
                    AverageRating = 0,
                    TotalReviews = 0
                });
            }
        }

        // POST: /reviews/create
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("Invalid model state for review creation: {@Errors}", errors);
                return Json(new
                {
                    success = false,
                    message = "Please fill in all required fields correctly.",
                    errors = errors
                });
            }

            try
            {
                var result = await _reviewService.CreateReviewAsync(dto);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Review created successfully for product {ProductId} by {Email}",
                        dto.ProductId, dto.GuestEmail);

                    return Json(new
                    {
                        success = true,
                        message = "Thank you for your review! It will be visible after approval."
                    });
                }
                else
                {
                    _logger.LogWarning("Failed to create review for product {ProductId}: {Error}",
                        dto.ProductId, result.ErrorMessage);

                    return Json(new
                    {
                        success = false,
                        message = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating review for product {ProductId}", dto.ProductId);
                return Json(new
                {
                    success = false,
                    message = "An error occurred while submitting your review. Please try again."
                });
            }
        }

        // GET: /reviews/count/{productId}
        [HttpGet("count/{productId}")]
        public async Task<IActionResult> GetReviewCount(int productId)
        {
            try
            {
                var result = await _reviewService.GetProductReviewsAsync(productId);

                if (result.Succeeded)
                {
                    return Content(result.Data.TotalReviews.ToString());
                }
                else
                {
                    return Content("0");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting review count for product {ProductId}", productId);
                return Content("0");
            }
        }
    }
}