using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Payment
{
    public class PaymentRequestDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EGP";
        public string OrderNumber { get; set; } = default!;
        public string CustomerEmail { get; set; } = default!;
        public string CustomerPhone { get; set; } = default!;
        public string CustomerName { get; set; } = default!;
    }
}
