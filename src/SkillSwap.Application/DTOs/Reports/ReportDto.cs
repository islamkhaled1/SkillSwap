using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Reports;

public sealed class ReportDto
{
    public long Id { get; init; }
    public Guid ReporterId { get; init; }
    public string ReporterDisplayName { get; init; } = string.Empty;
    public Guid ReportedUserId { get; init; }
    public string ReportedUserDisplayName { get; init; } = string.Empty;
    public long? SessionId { get; init; }
    public ReportReason Reason { get; init; }
    public string? Description { get; init; }
    public ReportStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
}
