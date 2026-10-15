using System.ComponentModel.DataAnnotations;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Reports;

public sealed class UpdateReportStatusRequest
{
    [Required]
    public ReportStatus Status { get; init; }
}
