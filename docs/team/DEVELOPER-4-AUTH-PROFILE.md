# Developer 4 — Auth & Profile

**Developer:** [Kholoud Khaled](https://github.com/kholoudkhaled191) (@kholoudkhaled191)  
**Role:** Developer 4  
**Module:** Authentication, Identity, Security & User Profiles  

---

## 1. Mission

Own the end-to-end identity, authentication, credential validation, JWT token issuance, security policies, role-based authorization infrastructure, and user profile management for SkillSwap. Provide a secure, reliable user onboarding experience and establish the core `Guid UserId` identity context utilized by all downstream modules.

---

## 2. Scope of Ownership

- ASP.NET Core Identity integration with Microsoft SQL Server.
- User registration and login workflows (`AuthController`).
- Password hashing, lockout policies, and security validation.
- JWT access token generation and claims configuration (`IJwtTokenService`, `JwtTokenService`).
- Production JWT configuration validation (`JwtSettingsValidator`).
- Current user context resolution (`ICurrentUserService`, `CurrentUserService`).
- User profile management (`UserProfile`, `IProfileService`, `ProfileService`, `ProfilesController`), including bio, avatar URL, time zone, and verification flag.
- Default account bootstrapping coordination (initial wallet and free subscription initialization during registration).
- Role configuration and seeding support (`Admin` and `User` roles).

---

## 3. Responsibilities

- Maintain `POST /api/auth/register` and `POST /api/auth/login`.
- Maintain `GET /api/profiles/me`, `PUT /api/profiles/me`, and `GET /api/profiles/{id}`.
- Ensure all JWT tokens embed correct standard claims (`sub` as user Guid, `email`, `jti`, and `role`).
- Maintain `CurrentUserService` to reliably extract `UserId` from `ClaimsPrincipal`.
- Coordinate user creation so that every new user receives an initial `Wallet` (0 Available, 0 Held) and an active `UserSubscription` (Free plan).
- Validate that production JWT secret keys meet 256-bit entropy requirements via `JwtSettingsValidator`.
- Write and maintain comprehensive unit and integration tests covering authentication, token validation, security policies, and profile CRUD.

---

## 4. Domain Entities Owned

| Entity | Primary Key | Table | Description |
|---|---|---|---|
| `ApplicationUser` | `Guid` | `Users` | Core identity record extending `IdentityUser<Guid>`. Includes soft-delete flags. |
| `UserProfile` | `Guid` | `UserProfiles` | Extended 1:1 profile record linked to `ApplicationUser`. Holds display name, bio, photo URL, timezone. |

---

## 5. Application Services & Infrastructure Owned

| Interface / Class | Location | Status | Description |
|---|---|---|---|
| `IJwtTokenService` | `src/SkillSwap.Application/Abstractions/IJwtTokenService.cs` | **IMPLEMENTED** | Generates signed JWT access tokens with user claims. |
| `JwtTokenService` | `src/SkillSwap.Infrastructure/Services/JwtTokenService.cs` | **IMPLEMENTED** | Infrastructure implementation using `JwtSettings`. |
| `JwtSettingsValidator` | `src/SkillSwap.Infrastructure/Services/JwtSettingsValidator.cs` | **IMPLEMENTED** | Validates JWT configuration entropy and formats at startup. |
| `ICurrentUserService` | `src/SkillSwap.Application/Abstractions/ICurrentUserService.cs` | **IMPLEMENTED** | Provides current authenticated `UserId`, `Email`, and roles. |
| `CurrentUserService` | `src/SkillSwap.Infrastructure/Services/CurrentUserService.cs` | **IMPLEMENTED** | Extracts claims from `IHttpContextAccessor`. |
| `IAuthService` | `src/SkillSwap.Application/Abstractions/IAuthService.cs` | **IMPLEMENTED** | Application contract for register, login, and token generation. |
| `AuthService` | `src/SkillSwap.Infrastructure/Services/AuthService.cs` | **IMPLEMENTED** | Orchestrates UserManager, SignInManager, profile, wallet, and plan creation. |
| `IProfileService` | `src/SkillSwap.Application/Abstractions/IProfileService.cs` | **IMPLEMENTED** | Application contract for querying and updating user profiles. |
| `ProfileService` | `src/SkillSwap.Application/Services/ProfileService.cs` | **IMPLEMENTED** | Profile retrieval, update, and statistics calculation. |

---

## 6. Controllers & Endpoints Owned

| Controller | Route Area | Method & Endpoint | Description |
|---|---|---|---|
| `AuthController` | `/api/auth` | `POST /api/auth/register` | Register new user account, profile, wallet, and free plan. |
| `AuthController` | `/api/auth` | `POST /api/auth/login` | Authenticate with email/password and receive JWT token. |
| `ProfilesController` | `/api/profiles` | `GET /api/profiles/me` | Retrieve authenticated user's profile and calculated stats. |
| `ProfilesController` | `/api/profiles` | `PUT /api/profiles/me` | Update authenticated user's profile information. |
| `ProfilesController` | `/api/profiles` | `GET /api/profiles/{id}` | Retrieve public profile by user Guid. |

---

## 7. Files Owned

### Entities & Configurations
- `src/SkillSwap.Infrastructure/Identity/ApplicationUser.cs`
- `src/SkillSwap.Domain/Entities/UserProfile.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/UserProfileConfiguration.cs`

### DTOs
- `src/SkillSwap.Application/DTOs/Auth/RegisterRequestDto.cs`
- `src/SkillSwap.Application/DTOs/Auth/LoginRequestDto.cs`
- `src/SkillSwap.Application/DTOs/Auth/AuthResponseDto.cs`
- `src/SkillSwap.Application/DTOs/Profiles/UserProfileDto.cs`
- `src/SkillSwap.Application/DTOs/Profiles/UpdateProfileRequestDto.cs`

### Services & Infrastructure
- `src/SkillSwap.Application/Abstractions/IAuthService.cs`
- `src/SkillSwap.Application/Abstractions/IProfileService.cs`
- `src/SkillSwap.Application/Abstractions/IJwtTokenService.cs`
- `src/SkillSwap.Application/Abstractions/ICurrentUserService.cs`
- `src/SkillSwap.Infrastructure/Services/AuthService.cs`
- `src/SkillSwap.Application/Services/ProfileService.cs`
- `src/SkillSwap.Infrastructure/Services/JwtTokenService.cs`
- `src/SkillSwap.Infrastructure/Services/JwtSettings.cs`
- `src/SkillSwap.Infrastructure/Services/JwtSettingsValidator.cs`
- `src/SkillSwap.Infrastructure/Services/CurrentUserService.cs`
- `src/SkillSwap.API/Extensions/AuthenticationExtensions.cs`

### Controllers
- `src/SkillSwap.API/Controllers/AuthController.cs`
- `src/SkillSwap.API/Controllers/ProfilesController.cs`

### Test Suites
- `tests/SkillSwap.Tests/Unit/AuthControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/AuthServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/ProfilesControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/ProfileServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/JwtConfigurationSecurityTests.cs`
- `tests/SkillSwap.Tests/Integration/AuthenticationIntegrationTests.cs`

---

## 8. Cross-Module Contracts & Dependencies

1. **User Identity Invariant:**
   - User primary key is strictly `Guid`.
   - All modules anchor user records to this `Guid`.
2. **Registration Bootstrapping Contract:**
   - When a user successfully registers:
     1. Insert `ApplicationUser` via `UserManager<ApplicationUser>`.
     2. Insert corresponding `UserProfile` (`DisplayName = request.DisplayName`).
     3. Insert corresponding `Wallet` (`AvailableMinutes = 0`, `HeldMinutes = 0`).
     4. Insert corresponding `UserSubscription` (`PlanId = FreePlanId`, `Status = Active`).
   - All 4 records are committed atomically.
3. **Claims Structure:**
   - `sub`: String representation of `Guid` (`user.Id.ToString()`).
   - `email`: User's normalized email.
   - `role`: Assigned application roles (`"User"`, `"Admin"`).
4. **Calculated Profile Statistics:**
   - Ratings and review counts must not be stored on `UserProfile`. They are calculated dynamically from `Reviews` (owned by Developer 2).
5. **Coordination with Developer 1:**
   - Wallet provisioning coordinates with Developer 1's financial contracts.
   - EF Core migrations are submitted to Developer 1 as entity configurations.

---
*Developer 4 Ownership Guide — SkillSwap Backend Engineering Team*
