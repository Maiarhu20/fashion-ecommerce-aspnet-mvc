using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Products
{
    public class ProductDeletionCheckDto
    {
        public bool CanDelete { get; set; }
        public string Message { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public List<string> BlockingReasons { get; set; } = new();
    }
}
