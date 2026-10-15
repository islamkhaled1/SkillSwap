using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.SwapRequests;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Abstractions;

public interface ISwapRequestService
{
    Task<Result<SwapRequestDto>> CreateSwapRequestAsync(
        Guid requesterId,
        CreateSwapRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SwapRequestDto>>> GetIncomingRequestsAsync(
        Guid receiverId,
        SwapRequestStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SwapRequestDto>>> GetOutgoingRequestsAsync(
        Guid requesterId,
        SwapRequestStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<Result<SwapRequestDto>> GetSwapRequestByIdAsync(
        Guid currentUserId,
        long id,
        CancellationToken cancellationToken = default);

    Task<Result<SwapRequestDto>> AcceptSwapRequestAsync(
        Guid receiverId,
        long id,
        CancellationToken cancellationToken = default);

    Task<Result<SwapRequestDto>> DeclineSwapRequestAsync(
        Guid receiverId,
        long id,
        CancellationToken cancellationToken = default);

    Task<Result<SwapRequestDto>> CancelSwapRequestAsync(
        Guid requesterId,
        long id,
        CancellationToken cancellationToken = default);

    Task<Result<int>> ExpireOldRequestsAsync(
        CancellationToken cancellationToken = default);
}
