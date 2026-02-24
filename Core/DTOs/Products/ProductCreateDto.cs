using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Products
{
    public class ProductCreateDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        [Required]
        public int CategoryId { get; set; }

        // Multiple images instead of single image
        public List<IFormFile>? ImageFiles { get; set; }
        public List<string>? ImageUrls { get; set; }

        [Range(0, 100)]
        public decimal? DiscountPercent { get; set; }

        // Color hex codes instead of color names
        [Required(ErrorMessage = "At least one color is required")]
        public List<string> ColorHexCodes { get; set; } = new List<string>();

        public List<string> ColorNames { get; set; } = new List<string>(); // Add this
    }
}