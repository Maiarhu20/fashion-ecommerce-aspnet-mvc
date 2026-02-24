using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Cart
{
    public class CartSummaryDTO
    {
        public int TotalItems { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public bool IsEmpty { get; set; } = true;
    }
}
