using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Reviews
{
    public class ReviewResponseDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductImageUrl { get; set; } // NEW: Product's primary image
        public string GuestName { get; set; }
        public string GuestEmail { get; set; }
        public int Rating { get; set; }
        public string Title { get; set; }
        public string Comment { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string RatingStars { get; set; }
    }
}