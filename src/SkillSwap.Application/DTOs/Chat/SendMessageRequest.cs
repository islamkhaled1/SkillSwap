using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Chat;

public sealed class SendMessageRequest
{
    [Required(ErrorMessage = "Message body cannot be empty.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message body must be between 1 and 2000 characters.")]
    public string Body { get; init; } = string.Empty;
}
