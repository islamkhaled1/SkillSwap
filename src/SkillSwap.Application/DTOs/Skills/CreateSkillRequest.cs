using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Application.DTOs.Skills
{
    public sealed class CreateSkillRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int CategoryId { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; init; } = string.Empty;
    }
}
