using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Core/DTOs/Reviews/ProductReviewsDto.cs
namespace Core.DTOs.Reviews
{
    public class ProductReviewsDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public List<ReviewResponseDto> Reviews { get; set; }
        public int ApprovedReviewsCount { get; set; }
        public int PendingReviewsCount { get; set; }
    }
}