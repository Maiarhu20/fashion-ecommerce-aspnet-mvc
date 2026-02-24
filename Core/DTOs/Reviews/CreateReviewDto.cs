using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Core/DTOs/Reviews/CreateReviewDto.cs
namespace Core.DTOs.Reviews
{
    public class CreateReviewDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required, MaxLength(100)]
        public string GuestName { get; set; }

        [Required, EmailAddress]
        public string GuestEmail { get; set; }

        [Required, Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(1000)]
        public string Comment { get; set; }
    }
}
