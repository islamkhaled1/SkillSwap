using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Subscriptions;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Tests.Unit;

public class SubscriptionsControllerTests
{
    private class FakeSubscriptionService : ISubscriptionService
    {
        public Func<Task<Result<IReadOnlyList<SubscriptionPlanDto>>>>? OnGetActivePlans { get; set; }
        public Func<Guid, Task<Result<UserSubscriptionDto>>>? OnGetCurrentSubscription { get; set; }
        public Func<Guid, SubscribeRequest, Task<Result<UserSubscriptionDto>>>? OnSubscribe { get; set; }

        public Task<Result<IReadOnlyList<SubscriptionPlanDto>>> GetActivePlansAsync(CancellationToken cancellationToken = default)
        {
            if (OnGetActivePlans != null) return OnGetActivePlans();
            return Task.FromResult(Result.Success<IReadOnlyList<SubscriptionPlanDto>>([]));
        }

        public Task<Result<UserSubscriptionDto>> GetCurrentSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (OnGetCurrentSubscription != null) return OnGetCurrentSubscription(userId);
            return Task.FromResult(Result.Success(new UserSubscriptionDto { UserId = userId, PlanCode = "FREE", Status = SubscriptionStatus.Active }));
        }

        public Task<Result<UserSubscriptionDto>> SubscribeAsync(Guid userId, SubscribeRequest request, CancellationToken cancellationToken = default)
        {
            if (OnSubscribe != null) return OnSubscribe(userId, request);
            return Task.FromResult(Result.Success(new UserSubscriptionDto { UserId = userId, PlanCode = request.PlanCode ?? "PREMIUM", Status = SubscriptionStatus.Active }));
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
    public async Task GetPlans_ReturnsOk()
    {
        var service = new FakeSubscriptionService
        {
            OnGetActivePlans = () => Task.FromResult(Result.Success<IReadOnlyList<SubscriptionPlanDto>>([
                new SubscriptionPlanDto { Id = 1, Code = "FREE", MonthlyLearningMinutes = 180, Price = 0.00m, IsActive = true },
                new SubscriptionPlanDto { Id = 2, Code = "PREMIUM", MonthlyLearningMinutes = 720, Price = null, IsActive = true }
            ]))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new SubscriptionsController(service, currentUserService);

        var result = await controller.GetPlans(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var plans = Assert.IsAssignableFrom<IReadOnlyList<SubscriptionPlanDto>>(ok.Value);
        Assert.Equal(2, plans.Count);
    }

    [Fact]
    public async Task GetMySubscription_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var service = new FakeSubscriptionService();
        var currentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new SubscriptionsController(service, currentUserService);

        var result = await controller.GetMySubscription(default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetMySubscription_WhenSuccessful_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var service = new FakeSubscriptionService
        {
            OnGetCurrentSubscription = uid => Task.FromResult(Result.Success(new UserSubscriptionDto { UserId = uid, PlanCode = "PREMIUM", Status = SubscriptionStatus.Active }))
        };
        var currentUserService = new FakeCurrentUserService { UserId = userId };
        var controller = new SubscriptionsController(service, currentUserService);

        var result = await controller.GetMySubscription(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var sub = Assert.IsType<UserSubscriptionDto>(ok.Value);
        Assert.Equal("PREMIUM", sub.PlanCode);
    }

    [Fact]
    public async Task Subscribe_WhenPlanNotFound_ReturnsNotFound()
    {
        var service = new FakeSubscriptionService
        {
            OnSubscribe = (_, _) => Task.FromResult(Result.Failure<UserSubscriptionDto>("Selected subscription plan was not found or is inactive."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new SubscriptionsController(service, currentUserService);

        var result = await controller.Subscribe(new SubscribeRequest { PlanCode = "NON_EXISTENT" }, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString());
    }

    [Fact]
    public async Task Subscribe_WhenSuccessful_ReturnsOk()
    {
        var service = new FakeSubscriptionService();
        var currentUserService = new FakeCurrentUserService();
        var controller = new SubscriptionsController(service, currentUserService);

        var result = await controller.Subscribe(new SubscribeRequest { PlanCode = "PREMIUM" }, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var sub = Assert.IsType<UserSubscriptionDto>(ok.Value);
        Assert.Equal("PREMIUM", sub.PlanCode);
    }
}
