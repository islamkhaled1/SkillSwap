using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Skills;

public sealed class CreateSkillRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid CategoryId is required.")]
    public int CategoryId { get; init; }

    [Required(ErrorMessage = "Skill name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Skill name must be between 1 and 100 characters.")]
    public string Name { get; init; } = string.Empty;
}
