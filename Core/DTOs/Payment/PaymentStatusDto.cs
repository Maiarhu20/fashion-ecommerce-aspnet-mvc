using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Payment
{
    public class PaymentStatusDto
    {
        public string ProviderName { get; set; } = default!;
        public string ProviderTransactionId { get; set; } = default!;
        public string Status { get; set; } = default!;
        public decimal Amount { get; set; }
    }

}
