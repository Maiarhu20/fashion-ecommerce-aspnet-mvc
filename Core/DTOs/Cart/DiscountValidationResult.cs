using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Cart
{
    // Add these supporting classes
    // Update the DiscountValidationResult class
    public class DiscountValidationResult
    {
        public bool IsValid { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DiscountPercentage { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        // Add these for easier tracking
        public int? DiscountId { get; set; }
        public string DiscountCode { get; set; } = string.Empty;
    }


}
