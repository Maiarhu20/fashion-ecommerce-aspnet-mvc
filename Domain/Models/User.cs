using Microsoft.AspNetCore.Identity;
using System;

namespace Domain.Models
{
    // Custom Identity user with extra properties.
    public class User : IdentityUser
    {
        // Optional display name
        public string? Name { get; set; }

        // When the account was created
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation to Admin profile (1:0..1)
        // NOTE: configured in DbContext as a one-to-one relation
        public virtual Admin? Admin { get; set; }
    }
}
