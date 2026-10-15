using System.ComponentModel.DataAnnotations;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Reports;

public sealed class CreateReportRequest
{
    [Required]
    public Guid ReportedUserId { get; init; }

    [Required]
    public ReportReason Reason { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    public long? SessionId { get; init; }
}
