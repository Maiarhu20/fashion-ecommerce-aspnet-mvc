using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Categories
{
    public class CategoryDeletionCheckDto
    {
        public bool CanDelete { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int ActiveOrderCount { get; set; }
        public List<string> BlockingReasons { get; set; } = new List<string>();
    }
}
