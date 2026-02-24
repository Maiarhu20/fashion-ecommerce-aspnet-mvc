using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        [Range(0.00, double.MaxValue)]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        [ForeignKey(nameof(Category))]
        public int CategoryId { get; set; }

        // REMOVED: public string ImageUrl { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal? DiscountPercent { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        [NotMapped]
        public decimal FinalPrice =>
            DiscountPercent.HasValue
                ? Math.Round(Price * (1 - (DiscountPercent.Value / 100m)), 2)
                : Price;

        // 🔵 Product can have many colors
        public virtual ICollection<ProductColor> Colors { get; set; } = new List<ProductColor>();

        // 🖼️ NEW: Product can have multiple images
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

        // Navigation properties
        public virtual Category Category { get; set; } = null!;
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}