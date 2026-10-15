namespace SkillSwap.Application.DTOs.Chat;

public sealed class ConversationDto
{
    public long Id { get; init; }
    public long SwapRequestId { get; init; }
    public int SkillId { get; init; }
    public string SkillName { get; init; } = string.Empty;
    public Guid OtherParticipantId { get; init; }
    public string OtherParticipantDisplayName { get; init; } = string.Empty;
    public string? OtherParticipantPhotoUrl { get; init; }
    public string? LastMessageBody { get; init; }
    public DateTime? LastMessageSentAtUtc { get; init; }
    public int UnreadCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
