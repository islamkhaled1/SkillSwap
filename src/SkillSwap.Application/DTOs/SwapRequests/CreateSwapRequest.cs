using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.SwapRequests;

public sealed class CreateSwapRequest
{
    [Required(ErrorMessage = "ReceiverId is required.")]
    public Guid ReceiverId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "A valid SkillId is required.")]
    public int SkillId { get; init; }

    [StringLength(1000, ErrorMessage = "Message must not exceed 1000 characters.")]
    public string? Message { get; init; }
}
