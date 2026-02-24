using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Models
{
    public class ShippingCity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string CityName { get; set; } = default!;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal ShippingCost { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastModified { get; set; }
    }
}
