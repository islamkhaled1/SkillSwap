using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Reports;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Abstractions;

public interface IReportService
{
    Task<Result<ReportDto>> CreateReportAsync(
        Guid reporterId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ReportDto>>> GetReportsAsync(
        ReportStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<Result<ReportDto>> UpdateReportStatusAsync(
        long reportId,
        ReportStatus status,
        CancellationToken cancellationToken = default);
}
