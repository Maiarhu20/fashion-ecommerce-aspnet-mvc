using System.ComponentModel.DataAnnotations;
using Core.DTOs.Reviews;
using Core.Services;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [Route("admin/reviews")]
    public class ReviewsController : Controller
    {
        private readonly ReviewService _reviewService;
        private readonly ILogger<ReviewsController> _logger;

        public ReviewsController(ReviewService reviewService, ILogger<ReviewsController> logger)
        {
            _reviewService = reviewService;
            _logger = logger;
        }

        // GET: /admin/reviews
        [HttpGet("")]
        public async Task<IActionResult> Index(string status = null)
        {
            try
            {
                //_logger.LogInformation("Index called with status parameter: '{Status}'", status ?? "null");
                _logger.LogInformation("=== INDEX ACTION CALLED ===");
                _logger.LogInformation("Status parameter: '{Status}'", status ?? "null");
                _logger.LogInformation("URL: {Url}", Request.Path + Request.QueryString);

                // Normalize and parse status
                ReviewStatus? reviewStatus = null;
                string normalizedStatus = null;

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (Enum.TryParse<ReviewStatus>(status, true, out var parsedStatus))
                    {
                        reviewStatus = parsedStatus;
                        normalizedStatus = parsedStatus.ToString(); // This ensures consistent casing
                        _logger.LogInformation("Parsed status to enum: {ReviewStatus}", reviewStatus);
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse status '{Status}' to ReviewStatus enum", status);
                    }
                }

                // Get ALL reviews first for counts
                var allReviewsResult = await _reviewService.GetAllReviewsAsync();

                if (!allReviewsResult.Succeeded)
                {
                    _logger.LogWarning("Failed to get all reviews for counts: {Error}", allReviewsResult.ErrorMessage);
                    TempData["ErrorMessage"] = "Failed to load review counts. Please try again.";

                    var filteredResult1 = await _reviewService.GetAllReviewsAsync(reviewStatus);
                    ViewBag.CurrentStatus = normalizedStatus;
                    ViewBag.TotalCount = 0;
                    ViewBag.PendingCount = 0;
                    ViewBag.ApprovedCount = 0;
                    ViewBag.RejectedCount = 0;
                    return View(filteredResult1.Data ?? new List<ReviewResponseDto>());
                }

                var allReviews = allReviewsResult.Data?.ToList() ?? new List<ReviewResponseDto>();

                // Calculate counts from all reviews
                var totalCount = allReviews.Count;
                var pendingCount = allReviews.Count(r => r.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase));
                var approvedCount = allReviews.Count(r => r.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase));
                var rejectedCount = allReviews.Count(r => r.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase));

                _logger.LogInformation("Review counts - Total: {Total}, Pending: {Pending}, Approved: {Approved}, Rejected: {Rejected}",
                    totalCount, pendingCount, approvedCount, rejectedCount);

                // Get filtered reviews
                _logger.LogInformation("Fetching filtered reviews with status: {Status}", reviewStatus?.ToString() ?? "All");
                var filteredResult = await _reviewService.GetAllReviewsAsync(reviewStatus);

                // Set ViewBag values
                ViewBag.CurrentStatus = normalizedStatus; // Will be "Pending", "Approved", "Rejected", or null
                ViewBag.TotalCount = totalCount;
                ViewBag.PendingCount = pendingCount;
                ViewBag.ApprovedCount = approvedCount;
                ViewBag.RejectedCount = rejectedCount;

                if (filteredResult.Succeeded)
                {
                    var filteredCount = filteredResult.Data?.Count() ?? 0;
                    _logger.LogInformation("Successfully retrieved {Count} filtered reviews", filteredCount);
                    return View(filteredResult.Data ?? new List<ReviewResponseDto>());
                }
                else
                {
                    _logger.LogWarning("Failed to get filtered reviews: {Error}", filteredResult.ErrorMessage);
                    TempData["ErrorMessage"] = "Failed to load reviews. Please try again.";
                    return View(new List<ReviewResponseDto>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reviews index with status: {Status}", status);
                TempData["ErrorMessage"] = "An error occurred while loading reviews.";
                ViewBag.CurrentStatus = null;
                ViewBag.TotalCount = 0;
                ViewBag.PendingCount = 0;
                ViewBag.ApprovedCount = 0;
                ViewBag.RejectedCount = 0;
                return View(new List<ReviewResponseDto>());
            }
        }

        // GET: /admin/reviews/details/{id}
        [HttpGet("details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var result = await _reviewService.GetReviewByIdAsync(id);

                if (result.Succeeded)
                {
                    return PartialView("_ReviewDetails", result.Data);
                }
                else
                {
                    _logger.LogWarning("Failed to get review {ReviewId}: {Error}", id, result.ErrorMessage);
                    return PartialView("_ReviewDetails", null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting review details for review {ReviewId}", id);
                return PartialView("_ReviewDetails", null);
            }
        }

        // POST: /admin/reviews/update-status
        [HttpPost("update-status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus([FromBody] ReviewStatusUpdateRequest request)
        {
            if (request == null || request.ReviewId <= 0 || string.IsNullOrEmpty(request.Status))
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid request data."
                });
            }

            // Parse the status string to enum
            if (!Enum.TryParse<ReviewStatus>(request.Status, true, out var reviewStatus))
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid status value."
                });
            }

            try
            {
                var dto = new ReviewStatusUpdateDto
                {
                    ReviewId = request.ReviewId,
                    Status = reviewStatus
                };

                var result = await _reviewService.UpdateReviewStatusAsync(dto);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Review {ReviewId} status updated to {Status}",
                        dto.ReviewId, dto.Status);

                    return Json(new
                    {
                        success = true,
                        message = $"Review status updated to {dto.Status} successfully.",
                        reviewId = dto.ReviewId,
                        newStatus = dto.Status.ToString()
                    });
                }
                else
                {
                    _logger.LogWarning("Failed to update review {ReviewId} status: {Error}",
                        dto.ReviewId, result.ErrorMessage);

                    return Json(new
                    {
                        success = false,
                        message = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating review {ReviewId} status", request.ReviewId);
                return Json(new
                {
                    success = false,
                    message = "An error occurred while updating review status."
                });
            }
        }

        // POST: /admin/reviews/delete/{id}
        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                _logger.LogInformation("=== DELETE ACTION CALLED ===");
                _logger.LogInformation("Review ID to delete: {ReviewId}", id);
                _logger.LogInformation("Is AJAX request: {IsAjax}",
                    Request.Headers["X-Requested-With"] == "XMLHttpRequest");

                var result = await _reviewService.DeleteReviewAsync(id);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Review {ReviewId} deleted successfully", id);

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new
                        {
                            success = true,
                            message = "Review deleted successfully.",
                            reviewId = id
                        });
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "Review deleted successfully.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to delete review {ReviewId}: {Error}", id, result.ErrorMessage);

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new
                        {
                            success = false,
                            message = result.ErrorMessage
                        });
                    }
                    else
                    {
                        TempData["ErrorMessage"] = result.ErrorMessage;
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting review {ReviewId}", id);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = false,
                        message = "An error occurred while deleting the review."
                    });
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the review.";
                    return RedirectToAction(nameof(Index));
                }
            }
        }


    }

    public class ReviewStatusUpdateRequest
    {
        public int ReviewId { get; set; }
        public string Status { get; set; }
    }
}