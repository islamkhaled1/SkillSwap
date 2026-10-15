using System.ComponentModel.DataAnnotations;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Skills;

public sealed class UpdateUserSkillRequest
{
    [EnumDataType(typeof(UserSkillLevel), ErrorMessage = "Valid UserSkillLevel (1=Beginner, 2=Intermediate, 3=Advanced) is required.")]
    public UserSkillLevel Level { get; init; }
}
