using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.SwapRequests;

public sealed class SwapRequestDto
{
    public long Id { get; init; }
    public Guid RequesterId { get; init; }
    public string RequesterDisplayName { get; init; } = string.Empty;
    public string? RequesterPhotoUrl { get; init; }
    public Guid ReceiverId { get; init; }
    public string ReceiverDisplayName { get; init; } = string.Empty;
    public string? ReceiverPhotoUrl { get; init; }
    public int SkillId { get; init; }
    public string SkillName { get; init; } = string.Empty;
    public string? Message { get; init; }
    public SwapRequestStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? RespondedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public long? ConversationId { get; init; }
}
