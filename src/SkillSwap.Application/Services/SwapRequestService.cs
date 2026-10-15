using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.Common.Options;
using SkillSwap.Application.DTOs.SwapRequests;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public sealed class SwapRequestService : ISwapRequestService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOptions<SessionPolicyOptions> _policyOptions;

    public SwapRequestService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        IOptions<SessionPolicyOptions> policyOptions)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _policyOptions = policyOptions;
    }

    public async Task<Result<SwapRequestDto>> CreateSwapRequestAsync(
        Guid requesterId,
        CreateSwapRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Result.Failure<SwapRequestDto>("Request cannot be null.");
        }

        if (requesterId == request.ReceiverId)
        {
            return Result.Failure<SwapRequestDto>("You cannot propose a swap request to yourself.");
        }

        if (request.SkillId <= 0)
        {
            return Result.Failure<SwapRequestDto>("A valid SkillId is required.");
        }

        var requesterProfileExists = await _context.UserProfiles
            .AnyAsync(p => p.UserId == requesterId, cancellationToken);
        if (!requesterProfileExists)
        {
            return Result.Failure<SwapRequestDto>($"Requester user profile {requesterId} was not found.");
        }

        var receiverProfileExists = await _context.UserProfiles
            .AnyAsync(p => p.UserId == request.ReceiverId, cancellationToken);
        if (!receiverProfileExists)
        {
            return Result.Failure<SwapRequestDto>($"Receiver user profile {request.ReceiverId} was not found.");
        }

        var skill = await _context.Skills
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == request.SkillId && s.IsActive && s.Category.IsActive, cancellationToken);
        if (skill == null)
        {
            return Result.Failure<SwapRequestDto>($"Active skill with ID {request.SkillId} was not found.");
        }

        // Two-way user-block check
        var isBlocked = await _context.UserBlocks
            .AnyAsync(b => (b.BlockerId == requesterId && b.BlockedId == request.ReceiverId) ||
                           (b.BlockerId == request.ReceiverId && b.BlockedId == requesterId),
                      cancellationToken);
        if (isBlocked)
        {
            return Result.Failure<SwapRequestDto>("Cannot propose a swap request due to user block restrictions.");
        }

        // Teacher qualification: Receiver must actively teach the requested skill
        var receiverTeachesSkill = await _context.UserSkills
            .AnyAsync(us => us.UserId == request.ReceiverId
                         && us.SkillId == request.SkillId
                         && (us.Type == UserSkillType.Teach || us.Type == UserSkillType.Teaching),
                      cancellationToken);
        if (!receiverTeachesSkill)
        {
            return Result.Failure<SwapRequestDto>($"Receiver {request.ReceiverId} is not an active teacher for skill '{skill.Name}'.");
        }

        // Prevent duplicate pending requests for the same skill and receiver
        var duplicatePending = await _context.SwapRequests
            .AnyAsync(sr => sr.RequesterId == requesterId
                         && sr.ReceiverId == request.ReceiverId
                         && sr.SkillId == request.SkillId
                         && sr.Status == SwapRequestStatus.Pending
                         && (sr.ExpiresAtUtc == null || sr.ExpiresAtUtc > _dateTimeProvider.UtcNow),
                      cancellationToken);
        if (duplicatePending)
        {
            return Result.Failure<SwapRequestDto>("A pending swap request for this skill and teacher already exists.");
        }

        var expirationDays = _policyOptions.Value.OldSwapRequestExpirationDays > 0
            ? _policyOptions.Value.OldSwapRequestExpirationDays
            : 7;

        var createdAtUtc = _dateTimeProvider.UtcNow;
        var expiresAtUtc = createdAtUtc.AddDays(expirationDays);

        var swapRequest = new SwapRequest
        {
            RequesterId = requesterId,
            ReceiverId = request.ReceiverId,
            SkillId = request.SkillId,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            Status = SwapRequestStatus.Pending,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };

        _context.SwapRequests.Add(swapRequest);

        var createPayload = JsonSerializer.Serialize(new
        {
            SwapRequestId = swapRequest.Id,
            RequesterId = requesterId,
            SkillId = request.SkillId,
            Message = $"New swap request from user {requesterId} for skill {skill.Name}"
        });

        _context.Notifications.Add(new Notification
        {
            UserId = request.ReceiverId,
            Type = NotificationKind.SwapRequest,
            Payload = createPayload,
            IsRead = false,
            CreatedAtUtc = createdAtUtc
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await MapToDtoAsync(swapRequest, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SwapRequestDto>>> GetIncomingRequestsAsync(
        Guid receiverId,
        SwapRequestStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SwapRequests
            .AsNoTracking()
            .Include(sr => sr.Skill)
            .Include(sr => sr.Conversation)
            .Where(sr => sr.ReceiverId == receiverId);

        if (status.HasValue)
        {
            query = query.Where(sr => sr.Status == status.Value);
        }

        var requests = await query
            .OrderByDescending(sr => sr.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await MapToDtoListAsync(requests, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SwapRequestDto>>> GetOutgoingRequestsAsync(
        Guid requesterId,
        SwapRequestStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SwapRequests
            .AsNoTracking()
            .Include(sr => sr.Skill)
            .Include(sr => sr.Conversation)
            .Where(sr => sr.RequesterId == requesterId);

        if (status.HasValue)
        {
            query = query.Where(sr => sr.Status == status.Value);
        }

        var requests = await query
            .OrderByDescending(sr => sr.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await MapToDtoListAsync(requests, cancellationToken);
    }

    public async Task<Result<SwapRequestDto>> GetSwapRequestByIdAsync(
        Guid currentUserId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var swapRequest = await _context.SwapRequests
            .Include(sr => sr.Conversation)
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);

        if (swapRequest == null)
        {
            return Result.Failure<SwapRequestDto>($"Swap request with ID {id} was not found.");
        }

        if (swapRequest.RequesterId != currentUserId && swapRequest.ReceiverId != currentUserId)
        {
            return Result.Failure<SwapRequestDto>("You are not authorized to view this swap request.");
        }

        return await MapToDtoAsync(swapRequest, cancellationToken);
    }

    public async Task<Result<SwapRequestDto>> AcceptSwapRequestAsync(
        Guid receiverId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var swapRequest = await _context.SwapRequests
            .Include(sr => sr.Conversation)
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);

        if (swapRequest == null)
        {
            return Result.Failure<SwapRequestDto>($"Swap request with ID {id} was not found.");
        }

        if (swapRequest.ReceiverId != receiverId)
        {
            return Result.Failure<SwapRequestDto>("Only the receiver can accept this swap request.");
        }

        if (swapRequest.Status != SwapRequestStatus.Pending)
        {
            return Result.Failure<SwapRequestDto>(
                $"Swap request must be in Pending status to be accepted. Current status: '{swapRequest.Status}'.");
        }

        if (swapRequest.ExpiresAtUtc.HasValue && swapRequest.ExpiresAtUtc.Value <= _dateTimeProvider.UtcNow)
        {
            swapRequest.Status = SwapRequestStatus.Expired;
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Failure<SwapRequestDto>("This swap request has expired.");
        }

        var existingConversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.SwapRequestId == id, cancellationToken);

        if (existingConversation != null)
        {
            return Result.Failure<SwapRequestDto>("A conversation already exists for this swap request.");
        }

        swapRequest.Status = SwapRequestStatus.Accepted;
        swapRequest.RespondedAtUtc = _dateTimeProvider.UtcNow;

        var conversation = new Conversation
        {
            SwapRequestId = swapRequest.Id,
            CreatedAtUtc = _dateTimeProvider.UtcNow,
            Participants =
            [
                new ConversationParticipant
                {
                    UserId = swapRequest.RequesterId,
                    JoinedAtUtc = _dateTimeProvider.UtcNow
                },
                new ConversationParticipant
                {
                    UserId = swapRequest.ReceiverId,
                    JoinedAtUtc = _dateTimeProvider.UtcNow
                }
            ]
        };

        _context.Conversations.Add(conversation);

        var acceptPayload = JsonSerializer.Serialize(new
        {
            SwapRequestId = swapRequest.Id,
            Status = "Accepted",
            Message = "Your swap request has been accepted."
        });

        _context.Notifications.Add(new Notification
        {
            UserId = swapRequest.RequesterId,
            Type = NotificationKind.SwapRequest,
            Payload = acceptPayload,
            IsRead = false,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<SwapRequestDto>("A conflict occurred while accepting the swap request.");
        }

        swapRequest.Conversation = conversation;
        return await MapToDtoAsync(swapRequest, cancellationToken);
    }

    public async Task<Result<SwapRequestDto>> DeclineSwapRequestAsync(
        Guid receiverId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var swapRequest = await _context.SwapRequests
            .Include(sr => sr.Conversation)
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);

        if (swapRequest == null)
        {
            return Result.Failure<SwapRequestDto>($"Swap request with ID {id} was not found.");
        }

        if (swapRequest.ReceiverId != receiverId)
        {
            return Result.Failure<SwapRequestDto>("Only the receiver can decline this swap request.");
        }

        if (swapRequest.Status != SwapRequestStatus.Pending)
        {
            return Result.Failure<SwapRequestDto>(
                $"Swap request must be in Pending status to be declined. Current status: '{swapRequest.Status}'.");
        }

        swapRequest.Status = SwapRequestStatus.Declined;
        swapRequest.RespondedAtUtc = _dateTimeProvider.UtcNow;

        var declinePayload = JsonSerializer.Serialize(new
        {
            SwapRequestId = swapRequest.Id,
            Status = "Declined",
            Message = "Your swap request has been declined."
        });

        _context.Notifications.Add(new Notification
        {
            UserId = swapRequest.RequesterId,
            Type = NotificationKind.SwapRequest,
            Payload = declinePayload,
            IsRead = false,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await MapToDtoAsync(swapRequest, cancellationToken);
    }

    public async Task<Result<SwapRequestDto>> CancelSwapRequestAsync(
        Guid requesterId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var swapRequest = await _context.SwapRequests
            .Include(sr => sr.Conversation)
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);

        if (swapRequest == null)
        {
            return Result.Failure<SwapRequestDto>($"Swap request with ID {id} was not found.");
        }

        if (swapRequest.RequesterId != requesterId)
        {
            return Result.Failure<SwapRequestDto>("Only the requester can cancel this swap request.");
        }

        if (swapRequest.Status != SwapRequestStatus.Pending)
        {
            return Result.Failure<SwapRequestDto>(
                $"Swap request must be in Pending status to be cancelled. Current status: '{swapRequest.Status}'.");
        }

        swapRequest.Status = SwapRequestStatus.Cancelled;
        swapRequest.CancelledAtUtc = _dateTimeProvider.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await MapToDtoAsync(swapRequest, cancellationToken);
    }

    public async Task<Result<int>> ExpireOldRequestsAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var oldRequests = await _context.SwapRequests
            .Where(sr => sr.Status == SwapRequestStatus.Pending && sr.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var req in oldRequests)
        {
            req.Status = SwapRequestStatus.Expired;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(oldRequests.Count);
    }

    private async Task<Result<SwapRequestDto>> MapToDtoAsync(SwapRequest sr, CancellationToken cancellationToken)
    {
        var userIds = new[] { sr.RequesterId, sr.ReceiverId };
        var profiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        profiles.TryGetValue(sr.RequesterId, out var requesterProfile);
        profiles.TryGetValue(sr.ReceiverId, out var receiverProfile);

        var skillName = sr.Skill?.Name;
        if (string.IsNullOrEmpty(skillName))
        {
            skillName = await _context.Skills
                .Where(s => s.Id == sr.SkillId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        }

        var conversationId = sr.Conversation?.Id;
        if (conversationId == null)
        {
            conversationId = await _context.Conversations
                .Where(c => c.SwapRequestId == sr.Id)
                .Select(c => (long?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Result.Success(new SwapRequestDto
        {
            Id = sr.Id,
            RequesterId = sr.RequesterId,
            RequesterDisplayName = requesterProfile?.DisplayName ?? $"User {sr.RequesterId.ToString()[..8]}",
            RequesterPhotoUrl = requesterProfile?.PhotoUrl,
            ReceiverId = sr.ReceiverId,
            ReceiverDisplayName = receiverProfile?.DisplayName ?? $"User {sr.ReceiverId.ToString()[..8]}",
            ReceiverPhotoUrl = receiverProfile?.PhotoUrl,
            SkillId = sr.SkillId,
            SkillName = skillName,
            Message = sr.Message,
            Status = sr.Status,
            CreatedAtUtc = sr.CreatedAtUtc,
            RespondedAtUtc = sr.RespondedAtUtc,
            ExpiresAtUtc = sr.ExpiresAtUtc,
            CancelledAtUtc = sr.CancelledAtUtc,
            ConversationId = conversationId
        });
    }

    private async Task<Result<IReadOnlyList<SwapRequestDto>>> MapToDtoListAsync(
        IReadOnlyList<SwapRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return Result.Success<IReadOnlyList<SwapRequestDto>>([]);
        }

        var allUserIds = requests.Select(r => r.RequesterId)
            .Concat(requests.Select(r => r.ReceiverId))
            .Distinct()
            .ToList();

        var profiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => allUserIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        var dtos = requests.Select(sr =>
        {
            profiles.TryGetValue(sr.RequesterId, out var requesterProfile);
            profiles.TryGetValue(sr.ReceiverId, out var receiverProfile);

            return new SwapRequestDto
            {
                Id = sr.Id,
                RequesterId = sr.RequesterId,
                RequesterDisplayName = requesterProfile?.DisplayName ?? $"User {sr.RequesterId.ToString()[..8]}",
                RequesterPhotoUrl = requesterProfile?.PhotoUrl,
                ReceiverId = sr.ReceiverId,
                ReceiverDisplayName = receiverProfile?.DisplayName ?? $"User {sr.ReceiverId.ToString()[..8]}",
                ReceiverPhotoUrl = receiverProfile?.PhotoUrl,
                SkillId = sr.SkillId,
                SkillName = sr.Skill?.Name ?? string.Empty,
                Message = sr.Message,
                Status = sr.Status,
                CreatedAtUtc = sr.CreatedAtUtc,
                RespondedAtUtc = sr.RespondedAtUtc,
                ExpiresAtUtc = sr.ExpiresAtUtc,
                CancelledAtUtc = sr.CancelledAtUtc,
                ConversationId = sr.Conversation?.Id
            };
        }).ToList();

        return Result.Success<IReadOnlyList<SwapRequestDto>>(dtos);
    }
}
