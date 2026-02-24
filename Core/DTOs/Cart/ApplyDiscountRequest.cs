using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Cart
{
    // Add this DTO for discount requests
    public class ApplyDiscountRequest
    {
        public string DiscountCode { get; set; } = string.Empty;
    }
}
