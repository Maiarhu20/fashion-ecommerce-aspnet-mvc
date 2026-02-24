using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Models
{
    public class HomeMedia
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(500)]
        public string MediaUrl { get; set; } = string.Empty;

        [Required]
        public MediaType MediaType { get; set; } = MediaType.Image;

        [StringLength(100)]
        public string? ButtonText { get; set; }

        [StringLength(500)]
        public string? ButtonLink { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }

    public enum MediaType
    {
        Image = 1,
        Video = 2
    }
}