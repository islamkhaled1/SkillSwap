using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Application.DTOs.Skills
{
    public sealed record SkillDto(
     int Id,
     string Name,
     int CategoryId,
     string CategoryName);
}
