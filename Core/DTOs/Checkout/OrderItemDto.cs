using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Checkout
{
    public class OrderItemDto
    {
        public string ProductName { get; set; } = default!;
        public string? SelectedColor { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
