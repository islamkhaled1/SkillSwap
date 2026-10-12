using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Wallets;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class AdminWalletsControllerTests
{
    private class FakeWalletService : IWalletService
    {
        public Func<Guid, int, string, string?, Task<Result<CreditTransactionDto>>>? OnApplyBonus { get; set; }

        public Task<Result<WalletDto>> GetWalletAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<IReadOnlyList<CreditTransactionDto>>> GetTransactionsAsync(Guid userId, int count = 50, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Wallet> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<CreditTransactionDto>> ApplyBonusAsync(Guid userId, int minutes, string description, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            if (OnApplyBonus != null) return OnApplyBonus(userId, minutes, description, idempotencyKey);
            return Task.FromResult(Result.Success(new CreditTransactionDto(1, userId, null, CreditTransactionType.Bonus, minutes, 0, DateTime.UtcNow, description)));
        }

        public Task<Result<CreditTransactionDto>> ApplyAdjustmentAsync(Guid userId, int availableDelta, int heldDelta, string description, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public void Controller_HasAuthorizeAdminAttribute()
    {
        var attributes = typeof(AdminWalletsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.NotEmpty(attributes);
        Assert.Equal("Admin", attributes[0].Roles);
    }

    [Fact]
    public async Task GrantBonus_WhenIdempotencyKeyMissing_ReturnsBadRequest()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var fakeService = new FakeWalletService();
        var controller = new AdminWalletsController(fakeService, context);

        var result = await controller.GrantBonus(Guid.NewGuid(), new GrantBonusRequest
        {
            Minutes = 60,
            Description = "Test grant"
        }, null, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Idempotency-Key header is required", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task GrantBonus_WhenIdempotencyKeyTooLong_ReturnsBadRequest()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var fakeService = new FakeWalletService();
        var controller = new AdminWalletsController(fakeService, context);

        var longKey = new string('x', 101);
        var result = await controller.GrantBonus(Guid.NewGuid(), new GrantBonusRequest
        {
            Minutes = 60,
            Description = "Test grant"
        }, longKey, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("cannot exceed 100 characters", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task GrantBonus_WhenUserNotFound_ReturnsNotFound()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var fakeService = new FakeWalletService();
        var controller = new AdminWalletsController(fakeService, context);

        var result = await controller.GrantBonus(Guid.NewGuid(), new GrantBonusRequest
        {
            Minutes = 60,
            Description = "Test grant"
        }, "key-123", default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString());
    }

    [Fact]
    public async Task GrantBonus_WhenPayloadNull_ReturnsBadRequest()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var fakeService = new FakeWalletService();
        var controller = new AdminWalletsController(fakeService, context);

        var result = await controller.GrantBonus(Guid.NewGuid(), null!, "key-123", default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GrantBonus_WhenServiceFails_ReturnsBadRequest()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "User 1", TimeZoneId = "UTC" });
        await context.SaveChangesAsync();

        var fakeService = new FakeWalletService
        {
            OnApplyBonus = (id, mins, desc, key) => Task.FromResult(Result.Failure<CreditTransactionDto>("Bonus minutes must be greater than zero."))
        };
        var controller = new AdminWalletsController(fakeService, context);

        var result = await controller.GrantBonus(userId, new GrantBonusRequest
        {
            Minutes = 0,
            Description = "Invalid"
        }, "key-123", default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("must be greater than zero", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task GrantBonus_WhenServiceReturnsConflict_Returns409Conflict()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "User 1", TimeZoneId = "UTC" });
        await context.SaveChangesAsync();

        var fakeService = new FakeWalletService
        {
            OnApplyBonus = (id, mins, desc, key) => Task.FromResult(Result.Failure<CreditTransactionDto>("Idempotency key conflict: already used."))
        };
        var controller = new AdminWalletsController(fakeService, context);

        var result = await controller.GrantBonus(userId, new GrantBonusRequest
        {
            Minutes = 60,
            Description = "Test"
        }, "key-conflict", default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("conflict", conflict.Value?.ToString());
    }

    [Fact]
    public async Task GrantBonus_WhenValid_ReturnsOkWithCreditTransaction()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "User 1", TimeZoneId = "UTC" });
        await context.SaveChangesAsync();

        var fakeService = new FakeWalletService();
        var controller = new AdminWalletsController(fakeService, context);

        var result = await controller.GrantBonus(userId, new GrantBonusRequest
        {
            Minutes = 120,
            Description = "Approved welcome bonus"
        }, "key-valid-123", default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var tx = Assert.IsType<CreditTransactionDto>(ok.Value);
        Assert.Equal(userId, tx.WalletId);
        Assert.Equal(120, tx.AvailableDelta);
        Assert.Equal(CreditTransactionType.Bonus, tx.Type);
    }
}
