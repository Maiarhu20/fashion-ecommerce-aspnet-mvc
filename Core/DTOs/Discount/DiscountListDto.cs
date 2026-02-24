using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Discount
{
    public class DiscountListDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Description { get; set; } = string.Empty;
        public string DiscountType { get; set; } = default!;
        public decimal DiscountValue { get; set; }
        public int? UsageLimitPerGuest { get; set; }
        public int TotalUsageCount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsCurrentlyActive { get; set; }
    }
}
