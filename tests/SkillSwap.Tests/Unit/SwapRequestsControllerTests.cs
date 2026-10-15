using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.SwapRequests;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Tests.Unit;

public class SwapRequestsControllerTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    private class FakeSwapRequestService : ISwapRequestService
    {
        public Func<Guid, CreateSwapRequest, Task<Result<SwapRequestDto>>>? OnCreateSwapRequest { get; set; }
        public Func<Guid, SwapRequestStatus?, Task<Result<IReadOnlyList<SwapRequestDto>>>>? OnGetIncomingRequests { get; set; }
        public Func<Guid, SwapRequestStatus?, Task<Result<IReadOnlyList<SwapRequestDto>>>>? OnGetOutgoingRequests { get; set; }
        public Func<Guid, long, Task<Result<SwapRequestDto>>>? OnGetSwapRequestById { get; set; }
        public Func<Guid, long, Task<Result<SwapRequestDto>>>? OnAcceptSwapRequest { get; set; }
        public Func<Guid, long, Task<Result<SwapRequestDto>>>? OnDeclineSwapRequest { get; set; }
        public Func<Guid, long, Task<Result<SwapRequestDto>>>? OnCancelSwapRequest { get; set; }

        public Task<Result<SwapRequestDto>> CreateSwapRequestAsync(Guid requesterId, CreateSwapRequest request, CancellationToken cancellationToken = default)
        {
            if (OnCreateSwapRequest != null)
                return OnCreateSwapRequest(requesterId, request);

            return Task.FromResult(Result.Success(new SwapRequestDto
            {
                Id = 1,
                RequesterId = requesterId,
                ReceiverId = request.ReceiverId,
                SkillId = request.SkillId,
                Status = SwapRequestStatus.Pending
            }));
        }

        public Task<Result<IReadOnlyList<SwapRequestDto>>> GetIncomingRequestsAsync(Guid receiverId, SwapRequestStatus? status = null, CancellationToken cancellationToken = default)
        {
            if (OnGetIncomingRequests != null)
                return OnGetIncomingRequests(receiverId, status);

            return Task.FromResult(Result.Success<IReadOnlyList<SwapRequestDto>>([
                new SwapRequestDto { Id = 1, RequesterId = Guid.NewGuid(), ReceiverId = receiverId, SkillId = 1, Status = SwapRequestStatus.Pending }
            ]));
        }

        public Task<Result<IReadOnlyList<SwapRequestDto>>> GetOutgoingRequestsAsync(Guid requesterId, SwapRequestStatus? status = null, CancellationToken cancellationToken = default)
        {
            if (OnGetOutgoingRequests != null)
                return OnGetOutgoingRequests(requesterId, status);

            return Task.FromResult(Result.Success<IReadOnlyList<SwapRequestDto>>([
                new SwapRequestDto { Id = 2, RequesterId = requesterId, ReceiverId = Guid.NewGuid(), SkillId = 2, Status = SwapRequestStatus.Pending }
            ]));
        }

        public Task<Result<SwapRequestDto>> GetSwapRequestByIdAsync(Guid currentUserId, long id, CancellationToken cancellationToken = default)
        {
            if (OnGetSwapRequestById != null)
                return OnGetSwapRequestById(currentUserId, id);

            return Task.FromResult(Result.Success(new SwapRequestDto
            {
                Id = id,
                RequesterId = currentUserId,
                ReceiverId = Guid.NewGuid(),
                SkillId = 1,
                Status = SwapRequestStatus.Pending
            }));
        }

        public Task<Result<SwapRequestDto>> AcceptSwapRequestAsync(Guid receiverId, long id, CancellationToken cancellationToken = default)
        {
            if (OnAcceptSwapRequest != null)
                return OnAcceptSwapRequest(receiverId, id);

            return Task.FromResult(Result.Success(new SwapRequestDto
            {
                Id = id,
                RequesterId = Guid.NewGuid(),
                ReceiverId = receiverId,
                SkillId = 1,
                Status = SwapRequestStatus.Accepted,
                ConversationId = 100
            }));
        }

        public Task<Result<SwapRequestDto>> DeclineSwapRequestAsync(Guid receiverId, long id, CancellationToken cancellationToken = default)
        {
            if (OnDeclineSwapRequest != null)
                return OnDeclineSwapRequest(receiverId, id);

            return Task.FromResult(Result.Success(new SwapRequestDto
            {
                Id = id,
                RequesterId = Guid.NewGuid(),
                ReceiverId = receiverId,
                SkillId = 1,
                Status = SwapRequestStatus.Declined
            }));
        }

        public Task<Result<SwapRequestDto>> CancelSwapRequestAsync(Guid requesterId, long id, CancellationToken cancellationToken = default)
        {
            if (OnCancelSwapRequest != null)
                return OnCancelSwapRequest(requesterId, id);

            return Task.FromResult(Result.Success(new SwapRequestDto
            {
                Id = id,
                RequesterId = requesterId,
                ReceiverId = Guid.NewGuid(),
                SkillId = 1,
                Status = SwapRequestStatus.Cancelled
            }));
        }

        public Task<Result<int>> ExpireOldRequestsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(0));
    }

    [Fact]
    public async Task CreateSwapRequest_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new SwapRequestsController(new FakeSwapRequestService(), fakeCurrentUserService);

        var response = await controller.CreateSwapRequest(new CreateSwapRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
    }

    [Fact]
    public async Task CreateSwapRequest_ValidRequest_ReturnsCreatedAtAction()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new SwapRequestsController(new FakeSwapRequestService(), fakeCurrentUserService);

        var request = new CreateSwapRequest { ReceiverId = Guid.NewGuid(), SkillId = 1 };
        var response = await controller.CreateSwapRequest(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response);
        var dto = Assert.IsType<SwapRequestDto>(created.Value);
        Assert.Equal(currentUserId, dto.RequesterId);
    }

    [Fact]
    public async Task AcceptSwapRequest_ByAuthorizedReceiver_ReturnsOk()
    {
        var receiverId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = receiverId };
        var controller = new SwapRequestsController(new FakeSwapRequestService(), fakeCurrentUserService);

        var response = await controller.AcceptSwapRequest(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var dto = Assert.IsType<SwapRequestDto>(ok.Value);
        Assert.Equal(SwapRequestStatus.Accepted, dto.Status);
        Assert.Equal(100, dto.ConversationId);
    }

    [Fact]
    public async Task AcceptSwapRequest_WhenForbidden_ReturnsForbid()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeService = new FakeSwapRequestService
        {
            OnAcceptSwapRequest = (_, _) => Task.FromResult(Result.Failure<SwapRequestDto>("Only the receiver can accept this swap request."))
        };
        var controller = new SwapRequestsController(fakeService, fakeCurrentUserService);

        var response = await controller.AcceptSwapRequest(1, CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
    }

    [Fact]
    public async Task CancelSwapRequest_WhenForbidden_ReturnsForbid()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeService = new FakeSwapRequestService
        {
            OnCancelSwapRequest = (_, _) => Task.FromResult(Result.Failure<SwapRequestDto>("Only the requester can cancel this swap request."))
        };
        var controller = new SwapRequestsController(fakeService, fakeCurrentUserService);

        var response = await controller.CancelSwapRequest(1, CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
    }
}
