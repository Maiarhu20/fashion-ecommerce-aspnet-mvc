using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class OrderItem
    {
        [Key]
        public int Id { get;  set; }

        [ForeignKey(nameof(Order))]
        public int OrderId { get;  set; }

        [ForeignKey(nameof(Product))]
        public int ProductId { get;  set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get;  set; }

        [MaxLength(50)]
        public string? SelectedColor { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(0.00, double.MaxValue)]
        public decimal UnitPrice { get;  set; } // Snapshot of product price

        [Column(TypeName = "decimal(10,2)")]
        public decimal LineTotal { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal? DiscountPercent { get; set; } // Snapshot discount at purchase time

        [NotMapped]
        public decimal DiscountedPrice =>
            DiscountPercent.HasValue
                ? Math.Round(UnitPrice * (1 - (DiscountPercent.Value / 100m)), 2)
                : UnitPrice;

        [NotMapped]
        public decimal FinalLineTotal => LineTotal * (DiscountPercent.HasValue ? (1 - (DiscountPercent.Value / 100m)) : 1);

        // Navigation
        public virtual Order Order { get;  set; }
        public virtual Product Product { get;  set; }
    }
}
