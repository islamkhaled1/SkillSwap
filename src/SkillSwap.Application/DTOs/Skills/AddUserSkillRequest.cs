using System.ComponentModel.DataAnnotations;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Skills;

public sealed class AddUserSkillRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid SkillId is required.")]
    public int SkillId { get; init; }

    [EnumDataType(typeof(UserSkillType), ErrorMessage = "Valid UserSkillType (1=Teach, 2=Learn) is required.")]
    public UserSkillType Type { get; init; }

    [EnumDataType(typeof(UserSkillLevel), ErrorMessage = "Valid UserSkillLevel (1=Beginner, 2=Intermediate, 3=Advanced) is required.")]
    public UserSkillLevel Level { get; init; }
}
