using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Models
{
    public class ProductImage
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        public bool IsPrimary { get; set; } = false;

        public int DisplayOrder { get; set; } = 0;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Product Product { get; set; } = null!;
    }
}