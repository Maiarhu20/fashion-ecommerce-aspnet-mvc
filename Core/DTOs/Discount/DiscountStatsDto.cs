using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Discount
{
    public class DiscountStatsDto
    {
        public int TotalDiscounts { get; set; }
        public int ActiveDiscounts { get; set; }
        public int ExpiredDiscounts { get; set; }
        public int UnusedDiscounts { get; set; }
        public int TotalUsageCount { get; set; }
        public string MostUsedDiscount { get; set; } = string.Empty;
    }
}
