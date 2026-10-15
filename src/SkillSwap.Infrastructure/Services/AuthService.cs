using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Auth;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Identity;
using SkillSwap.Infrastructure.Persistence;

namespace SkillSwap.Infrastructure.Services;

/// <summary>
/// Infrastructure service implementing authentication, atomic user registration, and credential validation.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext context,
        IJwtTokenService jwtTokenService,
        IDateTimeProvider dateTimeProvider,
        IOptions<JwtSettings> jwtOptions,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _jwtTokenService = jwtTokenService;
        _dateTimeProvider = dateTimeProvider;
        _jwtSettings = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<Result<AuthResponseDto>> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result.Failure<AuthResponseDto>("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return Result.Failure<AuthResponseDto>("Password is required.");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Result.Failure<AuthResponseDto>("DisplayName is required.");

        var trimmedDisplayName = request.DisplayName.Trim();
        if (trimmedDisplayName.Length > 100)
            return Result.Failure<AuthResponseDto>("DisplayName must not exceed 100 characters.");

        var trimmedEmail = request.Email.Trim();

        // 1. Check for duplicate email before proceeding
        var existingUser = await _userManager.FindByEmailAsync(trimmedEmail);
        if (existingUser != null)
        {
            return Result.Failure<AuthResponseDto>("A user with this email already exists.");
        }

        // 2. Verify Free subscription plan exists
        var freePlan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(sp => sp.Code == "FREE" && sp.IsActive, cancellationToken);

        if (freePlan == null)
        {
            return Result.Failure<AuthResponseDto>("Default 'FREE' subscription plan was not found or is inactive.");
        }

        // 3. Begin atomic database transaction
        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // InMemory test store without transaction warning configured
            transaction = null;
        }

        try
        {
            var now = _dateTimeProvider.UtcNow;

            // 4. Create Identity ApplicationUser
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = trimmedEmail,
                Email = trimmedEmail,
                LockoutEnabled = true,
                CreatedAtUtc = now
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                return Result.Failure<AuthResponseDto>(errors);
            }

            // 5. Ensure "User" role exists and assign it
            if (!await _roleManager.RoleExistsAsync("User"))
            {
                var roleCreateResult = await _roleManager.CreateAsync(new IdentityRole<Guid>("User"));
                if (!roleCreateResult.Succeeded)
                {
                    if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<AuthResponseDto>("Failed to initialize user role.");
                }
            }

            var addRoleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!addRoleResult.Succeeded)
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                var errors = string.Join("; ", addRoleResult.Errors.Select(e => e.Description));
                return Result.Failure<AuthResponseDto>(errors);
            }

            // 6. Create UserProfile
            var profile = new UserProfile
            {
                UserId = user.Id,
                DisplayName = trimmedDisplayName,
                TimeZoneId = "UTC",
                IsVerified = false,
                CreatedAtUtc = now
            };
            _context.UserProfiles.Add(profile);

            // 7. Create Wallet (AvailableMinutes = 0, HeldMinutes = 0, zero ledger transactions)
            var wallet = new Wallet
            {
                UserId = user.Id,
                AvailableMinutes = 0,
                HeldMinutes = 0,
                UpdatedAtUtc = now
            };
            _context.Wallets.Add(wallet);

            // 8. Create UserSubscription for Free plan
            var subscription = new UserSubscription
            {
                UserId = user.Id,
                PlanId = freePlan.Id,
                StartsAtUtc = now,
                EndsAtUtc = null,
                Status = SubscriptionStatus.Active,
                CreatedAtUtc = now
            };
            _context.UserSubscriptions.Add(subscription);

            // 9. Persist all related entities atomically
            await _context.SaveChangesAsync(cancellationToken);

            // 10. Commit transaction
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            // 11. Issue JWT token
            var roles = new[] { "User" };
            var token = await _jwtTokenService.GenerateTokenAsync(user.Id, user.Email!, roles, cancellationToken);
            var expiresAtUtc = now.AddMinutes(_jwtSettings.ExpiryMinutes > 0 ? _jwtSettings.ExpiryMinutes : 60);

            return Result.Success(new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email!,
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresAtUtc = expiresAtUtc,
                Roles = roles
            });
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogWarning(rollbackEx, "Failed to roll back transaction during registration failure.");
                }
            }

            _logger.LogError(ex, "Unexpected error occurred during registration for email {Email}", trimmedEmail);
            return Result.Failure<AuthResponseDto>($"Registration failed: {ex.Message}");
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result.Failure<AuthResponseDto>("Email and password are required.");
        }

        var trimmedEmail = request.Email.Trim();

        // 1. Find user using normalized email handling
        var user = await _userManager.FindByEmailAsync(trimmedEmail);
        if (user == null || user.IsDeleted)
        {
            // Reject without revealing user existence or deletion status
            return Result.Failure<AuthResponseDto>("Invalid email or password.");
        }

        // 2. Reject locked-out accounts
        if (await _userManager.IsLockedOutAsync(user))
        {
            return Result.Failure<AuthResponseDto>("Account is locked due to multiple failed login attempts. Please try again later.");
        }

        // 3. Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            if (_userManager.SupportsUserLockout)
            {
                await _userManager.AccessFailedAsync(user);
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                return Result.Failure<AuthResponseDto>("Account is locked due to multiple failed login attempts. Please try again later.");
            }

            return Result.Failure<AuthResponseDto>("Invalid email or password.");
        }

        // 4. Reset failed access count on success
        if (_userManager.SupportsUserLockout)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        // 5. Retrieve actual Identity roles
        var roles = await _userManager.GetRolesAsync(user);

        // 6. Issue JWT token
        var token = await _jwtTokenService.GenerateTokenAsync(user.Id, user.Email ?? trimmedEmail, roles, cancellationToken);
        var expiresAtUtc = _dateTimeProvider.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes > 0 ? _jwtSettings.ExpiryMinutes : 60);

        return Result.Success(new AuthResponseDto
        {
            UserId = user.Id,
            Email = user.Email ?? trimmedEmail,
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAtUtc,
            Roles = roles.ToList()
        });
    }
}
