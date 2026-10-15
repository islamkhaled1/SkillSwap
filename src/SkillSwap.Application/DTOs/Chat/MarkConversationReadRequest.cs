using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Chat;

public sealed class MarkConversationReadRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "A valid LastReadMessageId is required.")]
    public long LastReadMessageId { get; init; }
}
