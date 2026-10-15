using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Reports;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Tests.Unit;

public class ReportsControllerTests
{
    private class FakeReportService : IReportService
    {
        public Func<Guid, CreateReportRequest, Task<Result<ReportDto>>>? OnCreateReport { get; set; }
        public Func<ReportStatus?, Task<Result<IReadOnlyList<ReportDto>>>>? OnGetReports { get; set; }
        public Func<long, ReportStatus, Task<Result<ReportDto>>>? OnUpdateReportStatus { get; set; }

        public Task<Result<ReportDto>> CreateReportAsync(Guid reporterId, CreateReportRequest request, CancellationToken cancellationToken = default)
        {
            if (OnCreateReport != null) return OnCreateReport(reporterId, request);
            return Task.FromResult(Result.Success(new ReportDto { Id = 1, ReporterId = reporterId, ReportedUserId = request.ReportedUserId, Reason = request.Reason, Status = ReportStatus.Open }));
        }

        public Task<Result<IReadOnlyList<ReportDto>>> GetReportsAsync(ReportStatus? status = null, CancellationToken cancellationToken = default)
        {
            if (OnGetReports != null) return OnGetReports(status);
            return Task.FromResult(Result.Success<IReadOnlyList<ReportDto>>([]));
        }

        public Task<Result<ReportDto>> UpdateReportStatusAsync(long reportId, ReportStatus status, CancellationToken cancellationToken = default)
        {
            if (OnUpdateReportStatus != null) return OnUpdateReportStatus(reportId, status);
            return Task.FromResult(Result.Success(new ReportDto { Id = reportId, Status = status }));
        }
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Email { get; set; } = "test@example.com";
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    [Fact]
    public async Task CreateReport_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var service = new FakeReportService();
        var currentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new ReportsController(service, currentUserService);

        var result = await controller.CreateReport(new CreateReportRequest { ReportedUserId = Guid.NewGuid(), Reason = ReportReason.Spam }, default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CreateReport_WhenSuccessful_ReturnsCreated()
    {
        var service = new FakeReportService();
        var currentUserService = new FakeCurrentUserService();
        var controller = new ReportsController(service, currentUserService);

        var result = await controller.CreateReport(new CreateReportRequest { ReportedUserId = Guid.NewGuid(), Reason = ReportReason.Spam }, default);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task GetReports_ReturnsOk()
    {
        var service = new FakeReportService
        {
            OnGetReports = status => Task.FromResult(Result.Success<IReadOnlyList<ReportDto>>([
                new ReportDto { Id = 1, Reason = ReportReason.Harassment, Status = ReportStatus.Open }
            ]))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new ReportsController(service, currentUserService);

        var result = await controller.GetReports(null, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var reports = Assert.IsAssignableFrom<IReadOnlyList<ReportDto>>(ok.Value);
        Assert.Single(reports);
    }

    [Fact]
    public async Task UpdateReportStatus_WhenNotFound_ReturnsNotFound()
    {
        var service = new FakeReportService
        {
            OnUpdateReportStatus = (id, _) => Task.FromResult(Result.Failure<ReportDto>($"Report with ID {id} was not found."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new ReportsController(service, currentUserService);

        var result = await controller.UpdateReportStatus(100, new UpdateReportStatusRequest { Status = ReportStatus.Resolved }, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString());
    }

    [Fact]
    public async Task UpdateReportStatus_WhenSuccessful_ReturnsOk()
    {
        var service = new FakeReportService();
        var currentUserService = new FakeCurrentUserService();
        var controller = new ReportsController(service, currentUserService);

        var result = await controller.UpdateReportStatus(1, new UpdateReportStatusRequest { Status = ReportStatus.Resolved }, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<ReportDto>(ok.Value);
        Assert.Equal(ReportStatus.Resolved, report.Status);
    }
}
