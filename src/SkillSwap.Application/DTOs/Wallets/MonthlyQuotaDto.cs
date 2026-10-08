namespace SkillSwap.Application.DTOs.Wallets;

/// <summary>
/// Learning quota status for a user for a specific month.
/// </summary>
public record MonthlyQuotaDto(
    Guid UserId,
    int Year,
    int Month,
    int UsedMinutes,
    int MonthlyLimitMinutes,
    int RemainingMinutes,
    string PlanName);
