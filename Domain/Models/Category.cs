using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Category
    {
        [Key]
        public int Id { get;  set; }

        [Required, MaxLength(100)]
        public string Name { get;  set; }

        [MaxLength(500)]
        public string Description { get;  set; }
        public string ImageUrl { get; set; }

        public bool IsDeleted { get; set; } = false;
        //public bool IsActive { get; private set; } = true;
        public DateTime CreatedDate { get;  set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
