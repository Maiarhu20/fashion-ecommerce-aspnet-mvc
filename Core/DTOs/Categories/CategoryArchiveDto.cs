using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Categories
{
    public class CategoryArchiveDto
    {
        public int CategoryId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool HideFromCatalog { get; set; } = true;
    }
}
