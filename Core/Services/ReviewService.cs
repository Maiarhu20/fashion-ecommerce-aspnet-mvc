using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DTOs.Reviews;
using Core.Services.Email;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class ReviewService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ReviewService> _logger;
        private readonly IEmailService _emailService;

        public ReviewService(IUnitOfWork unitOfWork, ILogger<ReviewService> logger, IEmailService emailService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        public async Task<ServiceResult<ReviewResponseDto>> CreateReviewAsync(CreateReviewDto dto)
        {
            if (dto == null)
                return ServiceResult<ReviewResponseDto>.Failure("Review data is required.");

            try
            {
                _logger.LogDebug("Creating review for product {ProductId} by {Email}",
                    dto.ProductId, dto.GuestEmail);

                var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found for review creation", dto.ProductId);
                    return ServiceResult<ReviewResponseDto>.Failure("Product not found.");
                }

                if (product.IsDeleted)
                {
                    _logger.LogWarning("Cannot review deleted product {ProductId}", dto.ProductId);
                    return ServiceResult<ReviewResponseDto>.Failure("Cannot review a deleted product.");
                }

                var review = new Review
                {
                    ProductId = dto.ProductId,
                    GuestName = dto.GuestName.Trim(),
                    GuestEmail = dto.GuestEmail.Trim().ToLower(),
                    Rating = dto.Rating,
                    Title = dto.Title?.Trim(),
                    Comment = dto.Comment?.Trim(),
                    Status = ReviewStatus.Pending,
                    CreatedDate = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _unitOfWork.Reviews.AddAsync(review);
                var saved = await _unitOfWork.SaveAsync();

                if (saved > 0)
                {
                    _logger.LogInformation("Review created successfully. ID: {ReviewId}, Product: {ProductId}",
                        review.Id, dto.ProductId);

                    // Get product image
                    var productImageUrl = await GetProductPrimaryImageAsync(dto.ProductId);

                    return ServiceResult<ReviewResponseDto>.Success(
                        MapToDto(review, product.Name, productImageUrl));
                }
                else
                {
                    _logger.LogError("Failed to save review to database");
                    return ServiceResult<ReviewResponseDto>.Failure(
                        "Failed to save review. Please try again.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while creating review for product {ProductId}",
                    dto.ProductId);
                return ServiceResult<ReviewResponseDto>.Failure(
                    "A database error occurred while saving your review.", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating review for product {ProductId}",
                    dto.ProductId);
                return ServiceResult<ReviewResponseDto>.Failure(
                    "An unexpected error occurred. Please try again.", ex);
            }
        }

        public async Task<ServiceResult<ReviewResponseDto>> UpdateReviewStatusAsync(ReviewStatusUpdateDto dto)
        {
            if (dto == null)
                return ServiceResult<ReviewResponseDto>.Failure("Update data is required.");

            try
            {
                _logger.LogDebug("Updating review {ReviewId} status to {Status}",
                    dto.ReviewId, dto.Status);

                var review = await _unitOfWork.Reviews.GetByIdAsync(dto.ReviewId);
                if (review == null)
                {
                    _logger.LogWarning("Review {ReviewId} not found for status update", dto.ReviewId);
                    return ServiceResult<ReviewResponseDto>.Failure("Review not found.");
                }

                if (review.IsDeleted)
                {
                    _logger.LogWarning("Cannot update status of deleted review {ReviewId}", dto.ReviewId);
                    return ServiceResult<ReviewResponseDto>.Failure("Cannot update a deleted review.");
                }

                var oldStatus = review.Status;

                if (oldStatus == dto.Status)
                {
                    _logger.LogWarning("Review {ReviewId} is already in status {Status}", dto.ReviewId, dto.Status);
                    var product = await _unitOfWork.Products.GetByIdAsync(review.ProductId);
                    var productName = product?.Name ?? "Unknown Product";
                    var productImageUrl = await GetProductPrimaryImageAsync(review.ProductId);
                    return ServiceResult<ReviewResponseDto>.Success(
                        MapToDto(review, productName, productImageUrl));
                }

                review.Status = dto.Status;

                if (dto.Status == ReviewStatus.Approved)
                {
                    review.ApprovedDate = DateTime.UtcNow;
                }

                _unitOfWork.Reviews.Update(review);
                var saved = await _unitOfWork.SaveAsync();

                if (saved > 0)
                {
                    _logger.LogInformation("Review {ReviewId} status updated from {OldStatus} to {NewStatus}",
                        dto.ReviewId, oldStatus, dto.Status);

                    var product = await _unitOfWork.Products.GetByIdAsync(review.ProductId);
                    var productName = product?.Name ?? "Unknown Product";
                    var productImageUrl = await GetProductPrimaryImageAsync(review.ProductId);

                    if (dto.Status == ReviewStatus.Approved)
                    {
                        try
                        {
                            _ = SendReviewApprovalEmailInBackground(review, productName);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "Failed to initiate email sending for review {ReviewId}", review.Id);
                        }
                    }

                    return ServiceResult<ReviewResponseDto>.Success(
                        MapToDto(review, productName, productImageUrl));
                }
                else
                {
                    _logger.LogWarning("No changes saved for review {ReviewId} status update", dto.ReviewId);
                    return ServiceResult<ReviewResponseDto>.Failure("No changes were saved.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating review {ReviewId} status", dto.ReviewId);
                return ServiceResult<ReviewResponseDto>.Failure(
                    "A database error occurred while updating the review.", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating review {ReviewId} status", dto.ReviewId);
                return ServiceResult<ReviewResponseDto>.Failure(
                    "An unexpected error occurred. Please try again.", ex);
            }
        }

        private async Task SendReviewApprovalEmailInBackground(Review review, string productName)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendReviewApprovalEmail(review, productName);
                        _logger.LogInformation("Background email sent successfully for review {ReviewId}", review.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background email sending failed for review {ReviewId}", review.Id);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate background email for review {ReviewId}", review.Id);
            }
        }

        public async Task<ServiceResult<ProductReviewsDto>> GetProductReviewsAsync(int productId, bool includePending = false)
        {
            try
            {
                _logger.LogDebug("Getting reviews for product {ProductId}, includePending: {IncludePending}",
                    productId, includePending);

                var product = await _unitOfWork.Products.GetByIdAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found when getting reviews", productId);
                    return ServiceResult<ProductReviewsDto>.Failure("Product not found.");
                }

                var productImageUrl = await GetProductPrimaryImageAsync(productId);

                var reviews = await _unitOfWork.Reviews.FindAsync(
                    r => r.ProductId == productId &&
                         (includePending || r.Status == ReviewStatus.Approved) &&
                         !r.IsDeleted,
                    new[] { "Product" });

                if (!reviews.Any())
                {
                    _logger.LogDebug("No reviews found for product {ProductId}", productId);
                    return ServiceResult<ProductReviewsDto>.Success(new ProductReviewsDto
                    {
                        ProductId = productId,
                        ProductName = product.Name,
                        AverageRating = 0,
                        TotalReviews = 0,
                        ApprovedReviewsCount = 0,
                        PendingReviewsCount = 0,
                        Reviews = new List<ReviewResponseDto>()
                    });
                }

                var approvedReviews = reviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
                var pendingReviews = reviews.Where(r => r.Status == ReviewStatus.Pending).ToList();

                var result = new ProductReviewsDto
                {
                    ProductId = productId,
                    ProductName = product.Name,
                    AverageRating = approvedReviews.Any() ?
                        Math.Round(approvedReviews.Average(r => r.Rating), 1) : 0,
                    TotalReviews = approvedReviews.Count,
                    ApprovedReviewsCount = approvedReviews.Count,
                    PendingReviewsCount = pendingReviews.Count,
                    Reviews = approvedReviews
                        .OrderByDescending(r => r.CreatedDate)
                        .Select(r => MapToDto(r, product.Name, productImageUrl))
                        .ToList()
                };

                _logger.LogDebug("Found {Count} reviews for product {ProductId}",
                    result.TotalReviews, productId);

                return ServiceResult<ProductReviewsDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reviews for product {ProductId}", productId);
                return ServiceResult<ProductReviewsDto>.Failure(
                    "An error occurred while loading reviews.", ex);
            }
        }

        public async Task<ServiceResult<IEnumerable<ReviewResponseDto>>> GetAllReviewsAsync(ReviewStatus? status = null)
        {
            try
            {
                _logger.LogDebug("Getting all reviews, status filter: {Status}", status?.ToString() ?? "All");

                IEnumerable<Review> reviews;

                if (status.HasValue)
                {
                    reviews = await _unitOfWork.Reviews.FindAsync(
                        r => r.Status == status.Value && !r.IsDeleted,
                        new[] { "Product" });

                    _logger.LogDebug("Found {Count} reviews with status {Status}",
                        reviews?.Count() ?? 0, status.Value);
                }
                else
                {
                    reviews = await _unitOfWork.Reviews.FindAsync(
                        r => !r.IsDeleted,
                        new[] { "Product" });

                    _logger.LogDebug("Found {Count} total reviews", reviews?.Count() ?? 0);
                }

                var dtos = new List<ReviewResponseDto>();

                if (reviews != null)
                {
                    foreach (var review in reviews.OrderByDescending(r => r.CreatedDate))
                    {
                        var productImageUrl = await GetProductPrimaryImageAsync(review.ProductId);
                        dtos.Add(MapToDto(review, review.Product?.Name ?? "Unknown Product", productImageUrl));
                    }
                }

                _logger.LogDebug("Returning {Count} reviews with status filter: {Status}",
                    dtos.Count, status?.ToString() ?? "All");

                return ServiceResult<IEnumerable<ReviewResponseDto>>.Success(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all reviews");
                return ServiceResult<IEnumerable<ReviewResponseDto>>.Failure(
                    "An error occurred while loading reviews.", ex);
            }
        }

        public async Task<ServiceResult<ReviewResponseDto>> GetReviewByIdAsync(int id)
        {
            try
            {
                _logger.LogDebug("Getting review by ID: {ReviewId}", id);

                var review = await _unitOfWork.Reviews.FindOneAsync(
                    r => r.Id == id && !r.IsDeleted,
                    new[] { "Product" });

                if (review == null)
                {
                    _logger.LogWarning("Review {ReviewId} not found", id);
                    return ServiceResult<ReviewResponseDto>.Failure("Review not found.");
                }

                var productImageUrl = await GetProductPrimaryImageAsync(review.ProductId);
                var dto = MapToDto(review, review.Product?.Name ?? "Unknown Product", productImageUrl);
                return ServiceResult<ReviewResponseDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting review {ReviewId}", id);
                return ServiceResult<ReviewResponseDto>.Failure(
                    "An error occurred while loading the review.", ex);
            }
        }

        public async Task<ServiceResult> DeleteReviewAsync(int id)
        {
            try
            {
                _logger.LogDebug("Hard deleting review {ReviewId}", id);

                var review = await _unitOfWork.Reviews.GetByIdAsync(id);
                if (review == null)
                {
                    _logger.LogWarning("Review {ReviewId} not found for deletion", id);
                    return ServiceResult.Failure("Review not found.");
                }

                _unitOfWork.Reviews.Delete(review);
                var saved = await _unitOfWork.SaveAsync();

                if (saved > 0)
                {
                    _logger.LogInformation("Review {ReviewId} hard deleted permanently", id);
                    return ServiceResult.Success;
                }
                else
                {
                    _logger.LogWarning("No changes saved when deleting review {ReviewId}", id);
                    return ServiceResult.Failure("Failed to delete the review.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting review {ReviewId}", id);
                return ServiceResult.Failure(
                    "A database error occurred while deleting the review.", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting review {ReviewId}", id);
                return ServiceResult.Failure(
                    "An unexpected error occurred. Please try again.", ex);
            }
        }

        #region Private Methods

        /// <summary>
        /// Gets the primary image URL for a product
        /// </summary>
        private async Task<string> GetProductPrimaryImageAsync(int productId)
        {
            try
            {
                var images = await _unitOfWork.ProductImages.FindAsync(
                    img => img.ProductId == productId);

                var imagesList = images.ToList();

                if (!imagesList.Any())
                    return string.Empty;

                // First try to get the primary image
                var primaryImage = imagesList.FirstOrDefault(img => img.IsPrimary);

                // If no primary image, get the first one by display order
                if (primaryImage == null)
                    primaryImage = imagesList.OrderBy(img => img.DisplayOrder).First();

                return primaryImage?.ImageUrl ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting primary image for product {ProductId}", productId);
                return string.Empty;
            }
        }

        private ReviewResponseDto MapToDto(Review review, string productName, string productImageUrl)
        {
            if (review == null)
                return null;

            return new ReviewResponseDto
            {
                Id = review.Id,
                ProductId = review.ProductId,
                ProductName = productName,
                ProductImageUrl = productImageUrl,
                GuestName = review.GuestName,
                GuestEmail = review.GuestEmail,
                Rating = review.Rating,
                Title = review.Title,
                Comment = review.Comment,
                Status = review.Status.ToString(),
                CreatedDate = review.CreatedDate,
                ApprovedDate = review.ApprovedDate,
                RatingStars = GenerateStarRating(review.Rating)
            };
        }

        private string GenerateStarRating(int rating)
        {
            return new string('★', rating) + new string('☆', 5 - rating);
        }

        private async Task SendReviewApprovalEmail(Review review, string productName)
        {
            try
            {
                var subject = $"Your Review Has Been Approved - {productName}";
                var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #912356 0%, #7a1d47 100%); color: white; padding: 20px; text-align: center; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 5px; }}
        .rating {{ color: #ffc107; font-size: 18px; }}
        .review-box {{ background: white; padding: 15px; border-left: 4px solid #912356; margin: 15px 0; }}
        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
        .button {{ background: #912356; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Review Approved!</h1>
        </div>
        <div class='content'>
            <p>Hello {review.GuestName},</p>
            <p>Great news! Your review for <strong>{productName}</strong> has been approved and is now live on our website.</p>
            
            <div class='review-box'>
                <div class='rating'>
                    {GenerateStarRating(review.Rating)}
                    <span style='color: #333; margin-left: 10px;'>{review.Rating}/5</span>
                </div>
                <h4 style='margin-top: 10px; color: #333;'>{review.Title}</h4>
                <p>{review.Comment}</p>
            </div>
            
            <p>Thank you for sharing your experience with our community. Your feedback helps other customers make informed decisions.</p>
            
            <p style='text-align: center; margin-top: 20px;'>
                <a href='' class='button' style='color : white;'>
                    View your review in Product Page
                </a>
            </p>
        </div>
        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} . All rights reserved.</p>
            <p>If you have any questions, please contact our support team.</p>
        </div>
    </div>
</body>
</html>";

                await _emailService.SendEmailAsync(review.GuestEmail, subject, htmlContent);
                _logger.LogInformation("Review approval email sent to {Email} for review {ReviewId}", review.GuestEmail, review.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send review approval email to {Email}", review.GuestEmail);
            }
        }

        #endregion
    }

    public static class StringExtensions
    {
        public static string Repeat(this string str, int count)
        {
            return string.Concat(Enumerable.Repeat(str, count));
        }
    }
}