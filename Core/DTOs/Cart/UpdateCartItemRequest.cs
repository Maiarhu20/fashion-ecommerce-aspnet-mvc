using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Cart
{
    public class UpdateCartItemRequest
    {
        [Required]
        public int CartItemId { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; }
    }
}
