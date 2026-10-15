using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Blocks;

namespace SkillSwap.Tests.Unit;

public class BlocksControllerTests
{
    private class FakeBlockService : IBlockService
    {
        public Func<Guid, Guid, Task<Result<BlockedUserDto>>>? OnBlockUser { get; set; }
        public Func<Guid, Guid, Task<Result>>? OnUnblockUser { get; set; }
        public Func<Guid, Task<Result<IReadOnlyList<BlockedUserDto>>>>? OnGetBlockedUsers { get; set; }

        public Task<Result<BlockedUserDto>> BlockUserAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
        {
            if (OnBlockUser != null) return OnBlockUser(blockerId, blockedId);
            return Task.FromResult(Result.Success(new BlockedUserDto { BlockedId = blockedId, DisplayName = "Blocked" }));
        }

        public Task<Result> UnblockUserAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
        {
            if (OnUnblockUser != null) return OnUnblockUser(blockerId, blockedId);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<IReadOnlyList<BlockedUserDto>>> GetBlockedUsersAsync(Guid blockerId, CancellationToken cancellationToken = default)
        {
            if (OnGetBlockedUsers != null) return OnGetBlockedUsers(blockerId);
            return Task.FromResult(Result.Success<IReadOnlyList<BlockedUserDto>>([]));
        }

        public Task<bool> IsBlockedAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Email { get; set; } = "test@example.com";
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    [Fact]
    public async Task BlockUser_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var service = new FakeBlockService();
        var currentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new BlocksController(service, currentUserService);

        var result = await controller.BlockUser(Guid.NewGuid(), default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task BlockUser_WhenAlreadyBlocked_ReturnsConflict()
    {
        var service = new FakeBlockService
        {
            OnBlockUser = (_, _) => Task.FromResult(Result.Failure<BlockedUserDto>("User is already blocked."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new BlocksController(service, currentUserService);

        var result = await controller.BlockUser(Guid.NewGuid(), default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("already", conflict.Value?.ToString());
    }

    [Fact]
    public async Task BlockUser_WhenSuccessful_ReturnsCreated()
    {
        var targetId = Guid.NewGuid();
        var service = new FakeBlockService();
        var currentUserService = new FakeCurrentUserService();
        var controller = new BlocksController(service, currentUserService);

        var result = await controller.BlockUser(targetId, default);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task UnblockUser_WhenNotFound_ReturnsNotFound()
    {
        var service = new FakeBlockService
        {
            OnUnblockUser = (_, _) => Task.FromResult(Result.Failure("Block record was not found."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new BlocksController(service, currentUserService);

        var result = await controller.UnblockUser(Guid.NewGuid(), default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString());
    }

    [Fact]
    public async Task UnblockUser_WhenSuccessful_ReturnsNoContent()
    {
        var service = new FakeBlockService();
        var currentUserService = new FakeCurrentUserService();
        var controller = new BlocksController(service, currentUserService);

        var result = await controller.UnblockUser(Guid.NewGuid(), default);

        Assert.IsType<NoContentResult>(result);
    }
}
