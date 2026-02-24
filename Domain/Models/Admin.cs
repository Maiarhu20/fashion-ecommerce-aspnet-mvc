using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Models
{
    // Admin profile stored alongside Identity User
    public class Admin
    {
        // Primary key for Admin table
        [Key]
        public int Id { get; set; }

        // FK to AspNetUsers.Id (Identity user Id is string)
        [ForeignKey(nameof(User))]
        public string UserId { get; set; } = default!;

        // When admin profile was created
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation to Identity user
        public virtual User User { get; set; } = default!;
    }
}
