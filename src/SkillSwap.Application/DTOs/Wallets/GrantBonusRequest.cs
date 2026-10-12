using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Wallets;

/// <summary>
/// Payload for administrative credit grant to a user's wallet.
/// </summary>
public sealed class GrantBonusRequest
{
    [Range(1, 10000, ErrorMessage = "Minutes must be between 1 and 10000.")]
    public int Minutes { get; init; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Description must be between 3 and 500 characters.")]
    public string Description { get; init; } = string.Empty;
}
