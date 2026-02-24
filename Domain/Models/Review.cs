using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Review
    {
        [Key]
        public int Id { get;  set; }
        [ForeignKey(nameof(Product))]
        public int ProductId { get;  set; }

        [Required, MaxLength(100)]
        public string GuestName { get;  set; }

        [Required, EmailAddress]
        public string GuestEmail { get;  set; }

        [Range(1, 5)]
        public int Rating { get;  set; }

        [MaxLength(200)]
        public string Title { get;  set; }

        [MaxLength(1000)]
        public string Comment { get;  set; }

        public ReviewStatus Status { get;  set; } = ReviewStatus.Pending;

        public DateTime CreatedDate { get;  set; } = DateTime.UtcNow;
        public DateTime? ApprovedDate { get;  set; }
        //public string ApprovedBy { get;  set; } // Admin who approved

        public bool IsDeleted { get;  set; } = false;

        // Navigation
        public virtual Product Product { get;  set; }
    }

    public enum ReviewStatus 
    { 
        Pending, 
        Approved, 
        Rejected 
    }
}
