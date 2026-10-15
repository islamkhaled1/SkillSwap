using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Reports;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public sealed class ReportService : IReportService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReportService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ReportDto>> CreateReportAsync(
        Guid reporterId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Result.Failure<ReportDto>("Request cannot be null.");
        }

        if (reporterId == request.ReportedUserId)
        {
            return Result.Failure<ReportDto>("You cannot report yourself.");
        }

        var reportedUserExists = await _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(p => p.UserId == request.ReportedUserId, cancellationToken);

        if (!reportedUserExists)
        {
            return Result.Failure<ReportDto>($"User with ID {request.ReportedUserId} was not found.");
        }

        if (!Enum.IsDefined(typeof(ReportReason), request.Reason))
        {
            return Result.Failure<ReportDto>("Invalid report reason specified.");
        }

        if (request.Description != null && request.Description.Length > 1000)
        {
            return Result.Failure<ReportDto>("Description must not exceed 1000 characters.");
        }

        if (request.SessionId.HasValue)
        {
            var sessionExists = await _context.Sessions
                .AsNoTracking()
                .AnyAsync(s => s.Id == request.SessionId.Value, cancellationToken);

            if (!sessionExists)
            {
                return Result.Failure<ReportDto>($"Session with ID {request.SessionId.Value} was not found.");
            }
        }

        var now = _dateTimeProvider.UtcNow;
        var report = new Report
        {
            ReporterId = reporterId,
            ReportedUserId = request.ReportedUserId,
            Reason = request.Reason,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SessionId = request.SessionId,
            Status = ReportStatus.Open,
            CreatedAtUtc = now
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        var reporterProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == reporterId, cancellationToken);

        var reportedProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == request.ReportedUserId, cancellationToken);

        return Result.Success(new ReportDto
        {
            Id = report.Id,
            ReporterId = reporterId,
            ReporterDisplayName = reporterProfile?.DisplayName ?? $"User {reporterId.ToString()[..8]}",
            ReportedUserId = request.ReportedUserId,
            ReportedUserDisplayName = reportedProfile?.DisplayName ?? $"User {request.ReportedUserId.ToString()[..8]}",
            SessionId = report.SessionId,
            Reason = report.Reason,
            Description = report.Description,
            Status = report.Status,
            CreatedAtUtc = report.CreatedAtUtc,
            ResolvedAtUtc = report.ResolvedAtUtc
        });
    }

    public async Task<Result<IReadOnlyList<ReportDto>>> GetReportsAsync(
        ReportStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Reports
            .AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var reports = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = reports.Select(r => r.ReporterId)
            .Concat(reports.Select(r => r.ReportedUserId))
            .Distinct()
            .ToList();

        var profiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        var dtos = reports.Select(r =>
        {
            profiles.TryGetValue(r.ReporterId, out var reporterProfile);
            profiles.TryGetValue(r.ReportedUserId, out var reportedProfile);

            return new ReportDto
            {
                Id = r.Id,
                ReporterId = r.ReporterId,
                ReporterDisplayName = reporterProfile?.DisplayName ?? $"User {r.ReporterId.ToString()[..8]}",
                ReportedUserId = r.ReportedUserId,
                ReportedUserDisplayName = reportedProfile?.DisplayName ?? $"User {r.ReportedUserId.ToString()[..8]}",
                SessionId = r.SessionId,
                Reason = r.Reason,
                Description = r.Description,
                Status = r.Status,
                CreatedAtUtc = r.CreatedAtUtc,
                ResolvedAtUtc = r.ResolvedAtUtc
            };
        }).ToList();

        return Result.Success<IReadOnlyList<ReportDto>>(dtos);
    }

    public async Task<Result<ReportDto>> UpdateReportStatusAsync(
        long reportId,
        ReportStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(ReportStatus), status))
        {
            return Result.Failure<ReportDto>("Invalid report status specified.");
        }

        var report = await _context.Reports
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report == null)
        {
            return Result.Failure<ReportDto>($"Report with ID {reportId} was not found.");
        }

        report.Status = status;

        if (status == ReportStatus.Resolved || status == ReportStatus.Rejected)
        {
            report.ResolvedAtUtc = _dateTimeProvider.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var reporterProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == report.ReporterId, cancellationToken);

        var reportedProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == report.ReportedUserId, cancellationToken);

        return Result.Success(new ReportDto
        {
            Id = report.Id,
            ReporterId = report.ReporterId,
            ReporterDisplayName = reporterProfile?.DisplayName ?? $"User {report.ReporterId.ToString()[..8]}",
            ReportedUserId = report.ReportedUserId,
            ReportedUserDisplayName = reportedProfile?.DisplayName ?? $"User {report.ReportedUserId.ToString()[..8]}",
            SessionId = report.SessionId,
            Reason = report.Reason,
            Description = report.Description,
            Status = report.Status,
            CreatedAtUtc = report.CreatedAtUtc,
            ResolvedAtUtc = report.ResolvedAtUtc
        });
    }
}
