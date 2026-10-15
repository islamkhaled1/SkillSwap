using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Chat;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public sealed class ChatService : IChatService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ChatService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<ConversationDto>>> GetUserConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Find conversations where user is a participant
        var userParticipants = await _context.ConversationParticipants
            .AsNoTracking()
            .Where(cp => cp.UserId == userId)
            .ToListAsync(cancellationToken);

        if (userParticipants.Count == 0)
        {
            return Result.Success<IReadOnlyList<ConversationDto>>([]);
        }

        var conversationIds = userParticipants.Select(cp => cp.ConversationId).ToList();

        var conversations = await _context.Conversations
            .AsNoTracking()
            .Include(c => c.SwapRequest)
                .ThenInclude(sr => sr.Skill)
            .Include(c => c.Participants)
            .Where(c => conversationIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        // Find all other participant user IDs
        var otherUserIds = conversations
            .SelectMany(c => c.Participants.Where(p => p.UserId != userId).Select(p => p.UserId))
            .Distinct()
            .ToList();

        var profiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => otherUserIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        // Load latest messages and unread counts for each conversation
        var messages = await _context.Messages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId) && !m.IsDeleted)
            .ToListAsync(cancellationToken);

        var messagesLookup = messages.ToLookup(m => m.ConversationId);
        var userPartLookup = userParticipants.ToDictionary(p => p.ConversationId);

        var dtos = new List<ConversationDto>();

        foreach (var conv in conversations)
        {
            var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != userId);
            var otherId = otherParticipant?.UserId ?? Guid.Empty;
            profiles.TryGetValue(otherId, out var otherProfile);

            var convMessages = messagesLookup[conv.Id].OrderByDescending(m => m.SentAtUtc).ToList();
            var lastMessage = convMessages.FirstOrDefault();

            userPartLookup.TryGetValue(conv.Id, out var myParticipant);
            var lastReadId = myParticipant?.LastReadMessageId ?? 0;

            var unreadCount = convMessages.Count(m => m.SenderId != userId && m.Id > lastReadId);

            dtos.Add(new ConversationDto
            {
                Id = conv.Id,
                SwapRequestId = conv.SwapRequestId,
                SkillId = conv.SwapRequest.SkillId,
                SkillName = conv.SwapRequest.Skill?.Name ?? string.Empty,
                OtherParticipantId = otherId,
                OtherParticipantDisplayName = otherProfile?.DisplayName ?? $"User {otherId.ToString()[..8]}",
                OtherParticipantPhotoUrl = otherProfile?.PhotoUrl,
                LastMessageBody = lastMessage?.Body,
                LastMessageSentAtUtc = lastMessage?.SentAtUtc,
                UnreadCount = unreadCount,
                CreatedAtUtc = conv.CreatedAtUtc
            });
        }

        var ordered = dtos
            .OrderByDescending(d => d.LastMessageSentAtUtc ?? d.CreatedAtUtc)
            .ToList();

        return Result.Success<IReadOnlyList<ConversationDto>>(ordered);
    }

    public async Task<Result<ConversationDto>> GetConversationByIdAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        var participant = await _context.ConversationParticipants
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId, cancellationToken);

        if (participant == null)
        {
            return Result.Failure<ConversationDto>("You are not a participant in this conversation.");
        }

        var conversation = await _context.Conversations
            .AsNoTracking()
            .Include(c => c.SwapRequest)
                .ThenInclude(sr => sr.Skill)
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation == null)
        {
            return Result.Failure<ConversationDto>($"Conversation with ID {conversationId} was not found.");
        }

        var otherParticipant = conversation.Participants.FirstOrDefault(p => p.UserId != userId);
        var otherId = otherParticipant?.UserId ?? Guid.Empty;

        var otherProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == otherId, cancellationToken);

        var lastMessage = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.SentAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var unreadCount = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId &&
                        !m.IsDeleted &&
                        m.SenderId != userId &&
                        (participant.LastReadMessageId == null || m.Id > participant.LastReadMessageId.Value))
            .CountAsync(cancellationToken);

        return Result.Success(new ConversationDto
        {
            Id = conversation.Id,
            SwapRequestId = conversation.SwapRequestId,
            SkillId = conversation.SwapRequest.SkillId,
            SkillName = conversation.SwapRequest.Skill?.Name ?? string.Empty,
            OtherParticipantId = otherId,
            OtherParticipantDisplayName = otherProfile?.DisplayName ?? $"User {otherId.ToString()[..8]}",
            OtherParticipantPhotoUrl = otherProfile?.PhotoUrl,
            LastMessageBody = lastMessage?.Body,
            LastMessageSentAtUtc = lastMessage?.SentAtUtc,
            UnreadCount = unreadCount,
            CreatedAtUtc = conversation.CreatedAtUtc
        });
    }

    public async Task<Result<IReadOnlyList<MessageDto>>> GetConversationMessagesAsync(
        Guid userId,
        long conversationId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var isParticipant = await IsParticipantAsync(userId, conversationId, cancellationToken);
        if (!isParticipant)
        {
            return Result.Failure<IReadOnlyList<MessageDto>>("You are not a participant in this conversation.");
        }

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var messages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderBy(m => m.SentAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return Result.Success<IReadOnlyList<MessageDto>>([]);
        }

        var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
        var profiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => senderIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        var dtos = messages.Select(m =>
        {
            profiles.TryGetValue(m.SenderId, out var profile);
            return new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderDisplayName = profile?.DisplayName ?? $"User {m.SenderId.ToString()[..8]}",
                SenderPhotoUrl = profile?.PhotoUrl,
                Body = m.Body,
                SentAtUtc = m.SentAtUtc
            };
        }).ToList();

        return Result.Success<IReadOnlyList<MessageDto>>(dtos);
    }

    public async Task<Result<MessageDto>> SendMessageAsync(
        Guid senderId,
        long conversationId,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure<MessageDto>("Message body cannot be empty.");
        }

        var trimmed = body.Trim();
        if (trimmed.Length > 2000)
        {
            return Result.Failure<MessageDto>("Message body must not exceed 2000 characters.");
        }

        var conversation = await _context.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation == null)
        {
            return Result.Failure<MessageDto>($"Conversation with ID {conversationId} was not found.");
        }

        var senderParticipant = conversation.Participants.FirstOrDefault(p => p.UserId == senderId);
        if (senderParticipant == null)
        {
            return Result.Failure<MessageDto>("You are not a participant in this conversation.");
        }

        var otherParticipant = conversation.Participants.FirstOrDefault(p => p.UserId != senderId);
        if (otherParticipant != null)
        {
            var isBlocked = await _context.UserBlocks
                .AnyAsync(b => (b.BlockerId == senderId && b.BlockedId == otherParticipant.UserId) ||
                               (b.BlockerId == otherParticipant.UserId && b.BlockedId == senderId),
                          cancellationToken);

            if (isBlocked)
            {
                return Result.Failure<MessageDto>("Cannot send message due to user block restrictions.");
            }
        }

        var now = _dateTimeProvider.UtcNow;
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Body = trimmed,
            SentAtUtc = now,
            IsDeleted = false
        };

        _context.Messages.Add(message);

        if (otherParticipant != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = otherParticipant.UserId,
                Type = NotificationKind.NewMessage,
                Payload = $"New message from user {senderId}",
                IsRead = false,
                CreatedAtUtc = now
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Sender automatically has read their own message
        senderParticipant.LastReadMessageId = message.Id;
        await _context.SaveChangesAsync(cancellationToken);

        var senderProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == senderId, cancellationToken);

        return Result.Success(new MessageDto
        {
            Id = message.Id,
            ConversationId = conversationId,
            SenderId = senderId,
            SenderDisplayName = senderProfile?.DisplayName ?? $"User {senderId.ToString()[..8]}",
            SenderPhotoUrl = senderProfile?.PhotoUrl,
            Body = message.Body,
            SentAtUtc = message.SentAtUtc
        });
    }

    public async Task<Result> MarkConversationAsReadAsync(
        Guid userId,
        long conversationId,
        long lastReadMessageId,
        CancellationToken cancellationToken = default)
    {
        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId, cancellationToken);

        if (participant == null)
        {
            return Result.Failure("You are not a participant in this conversation.");
        }

        var messageExists = await _context.Messages
            .AnyAsync(m => m.Id == lastReadMessageId && m.ConversationId == conversationId, cancellationToken);

        if (!messageExists)
        {
            return Result.Failure("Specified message does not belong to this conversation.");
        }

        if (participant.LastReadMessageId == null || lastReadMessageId > participant.LastReadMessageId.Value)
        {
            participant.LastReadMessageId = lastReadMessageId;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<bool> IsParticipantAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId, cancellationToken);
    }
}
