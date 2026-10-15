using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.DTOs.Reports;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class ReportServiceTests
{
    private (ReportService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        var service = new ReportService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task CreateReport_WhenSelfReport_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var userId = Guid.NewGuid();

        var result = await service.CreateReportAsync(userId, new CreateReportRequest
        {
            ReportedUserId = userId,
            Reason = ReportReason.Harassment,
            Description = "self report"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot report yourself", result.Error);
    }

    [Fact]
    public async Task CreateReport_WhenReportedUserNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var reporterId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();

        var result = await service.CreateReportAsync(reporterId, new CreateReportRequest
        {
            ReportedUserId = reportedId,
            Reason = ReportReason.Spam,
            Description = "spam"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task CreateReport_WhenDescriptionExceeds1000Chars_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var reporterId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = reporterId, DisplayName = "Reporter" },
            new UserProfile { UserId = reportedId, DisplayName = "Reported" }
        );
        await context.SaveChangesAsync();

        var result = await service.CreateReportAsync(reporterId, new CreateReportRequest
        {
            ReportedUserId = reportedId,
            Reason = ReportReason.InappropriateContent,
            Description = new string('x', 1001)
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("1000 characters", result.Error);
    }

    [Fact]
    public async Task CreateReport_WhenSessionNotFound_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var reporterId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = reporterId, DisplayName = "Reporter" },
            new UserProfile { UserId = reportedId, DisplayName = "Reported" }
        );
        await context.SaveChangesAsync();

        var result = await service.CreateReportAsync(reporterId, new CreateReportRequest
        {
            ReportedUserId = reportedId,
            Reason = ReportReason.NoShow,
            SessionId = 999
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task CreateReport_WhenValid_CreatesReportSuccessfully()
    {
        var (service, context, dtp) = CreateTestHarness();
        var reporterId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = reporterId, DisplayName = "Reporter" },
            new UserProfile { UserId = reportedId, DisplayName = "Reported" }
        );
        await context.SaveChangesAsync();

        var result = await service.CreateReportAsync(reporterId, new CreateReportRequest
        {
            ReportedUserId = reportedId,
            Reason = ReportReason.Harassment,
            Description = "Unwanted behavior during session"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ReportReason.Harassment, result.Value.Reason);
        Assert.Equal(ReportStatus.Open, result.Value.Status);
        Assert.Equal(dtp.UtcNow, result.Value.CreatedAtUtc);
        Assert.Null(result.Value.ResolvedAtUtc);

        var report = await context.Reports.FirstOrDefaultAsync(r => r.ReporterId == reporterId && r.ReportedUserId == reportedId);
        Assert.NotNull(report);
    }

    [Fact]
    public async Task UpdateReportStatus_WhenResolved_SetsResolvedAtUtc()
    {
        var (service, context, dtp) = CreateTestHarness();
        var reporterId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = reporterId, DisplayName = "Reporter" },
            new UserProfile { UserId = reportedId, DisplayName = "Reported" }
        );

        var report = new Report
        {
            ReporterId = reporterId,
            ReportedUserId = reportedId,
            Reason = ReportReason.Harassment,
            Status = ReportStatus.Open,
            CreatedAtUtc = dtp.UtcNow.AddHours(-3)
        };
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        var result = await service.UpdateReportStatusAsync(report.Id, ReportStatus.Resolved);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReportStatus.Resolved, result.Value!.Status);
        Assert.Equal(dtp.UtcNow, result.Value.ResolvedAtUtc);
    }
}
