using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Checkout
{
    public class ShippingCityDto
    {
        public int Id { get; set; }
        public string CityName { get; set; } = default!;
        public decimal ShippingCost { get; set; }
    }
}
