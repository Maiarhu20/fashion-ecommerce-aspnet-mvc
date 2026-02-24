using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Models
{
    public class ProductColor
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }

        // 🔵 The color name (example: Red, Black, Cream)
        [Required, MaxLength(50)]
        public string ColorName { get; set; } = string.Empty;

        // 🎨 NEW: Hex color code (example: #FF0000, #000000)
        [Required, MaxLength(7)]
        public string ColorHexCode { get; set; } = string.Empty;

        // Navigation to Product
        public virtual Product Product { get; set; } = null!;
    }
}