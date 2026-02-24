using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Models;

namespace Core.DTOs.Reviews
{
    public class ReviewStatusUpdateDto
    {
        [Required]
        public int ReviewId { get; set; }

        [Required]
        public ReviewStatus Status { get; set; }
    }
}