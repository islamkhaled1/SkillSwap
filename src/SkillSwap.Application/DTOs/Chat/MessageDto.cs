namespace SkillSwap.Application.DTOs.Chat;

public sealed class MessageDto
{
    public long Id { get; init; }
    public long ConversationId { get; init; }
    public Guid SenderId { get; init; }
    public string SenderDisplayName { get; init; } = string.Empty;
    public string? SenderPhotoUrl { get; init; }
    public string Body { get; init; } = string.Empty;
    public DateTime SentAtUtc { get; init; }
}
