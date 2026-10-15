using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Chat;

namespace SkillSwap.Application.Abstractions;

public interface IChatService
{
    Task<Result<IReadOnlyList<ConversationDto>>> GetUserConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<ConversationDto>> GetConversationByIdAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<MessageDto>>> GetConversationMessagesAsync(
        Guid userId,
        long conversationId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<Result<MessageDto>> SendMessageAsync(
        Guid senderId,
        long conversationId,
        string body,
        CancellationToken cancellationToken = default);

    Task<Result> MarkConversationAsReadAsync(
        Guid userId,
        long conversationId,
        long lastReadMessageId,
        CancellationToken cancellationToken = default);

    Task<bool> IsParticipantAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default);
}
