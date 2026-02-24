using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Shipping
{
    public class UpdateShippingCityDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "City name is required")]
        [MaxLength(100, ErrorMessage = "City name cannot exceed 100 characters")]
        public string CityName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Shipping cost is required")]
        [Range(0, 10000, ErrorMessage = "Shipping cost must be between 0 and 10,000")]
        public decimal ShippingCost { get; set; }

        public bool IsActive { get; set; }
    }
}
