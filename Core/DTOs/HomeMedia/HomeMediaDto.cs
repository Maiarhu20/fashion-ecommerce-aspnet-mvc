using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Core.DTOs.HomeMedia
{
    public class HomeMediaDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public string MediaUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = "Image";
        public string? ButtonText { get; set; }
        public string? ButtonLink { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class CreateHomeMediaDto
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please select a media file")]
        public IFormFile? MediaFile { get; set; }

        [Required(ErrorMessage = "Please select media type")]
        public string MediaType { get; set; } = "Image";

        [StringLength(100, ErrorMessage = "Button text cannot exceed 100 characters")]
        public string? ButtonText { get; set; }

        [StringLength(500, ErrorMessage = "Button link cannot exceed 500 characters")]
        public string? ButtonLink { get; set; }

        [Range(0, 100, ErrorMessage = "Display order must be between 0 and 100")]
        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }

    public class UpdateHomeMediaDto
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        public IFormFile? MediaFile { get; set; }
        public string? CurrentMediaUrl { get; set; }

        [Required(ErrorMessage = "Please select media type")]
        public string MediaType { get; set; } = "Image";

        [StringLength(100, ErrorMessage = "Button text cannot exceed 100 characters")]
        public string? ButtonText { get; set; }

        [StringLength(500, ErrorMessage = "Button link cannot exceed 500 characters")]
        public string? ButtonLink { get; set; }

        [Range(0, 100, ErrorMessage = "Display order must be between 0 and 100")]
        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }

    public class HomeMediaListDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = "Image";
        public string? ButtonText { get; set; }
        public string? ButtonLink { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}