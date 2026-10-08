# Member 1 -- Auth & Profile

## 1. Mission
Own the end-to-end identity, authentication, credential validation, JWT token issuance, role-based authorization infrastructure, and user profile management for SkillSwap. Provide a secure, reliable user onboarding experience and establish the core `Guid UserId` identity context utilized by all downstream modules.

---

## 2. Scope of Ownership
- ASP.NET Core Identity integration with SQL Server.
- User registration and login workflows.
- Password hashing, lockout policies, and security validation.
- JWT access token generation and claims configuration.
- Current user context resolution (`ICurrentUserService`).
- User profile management (`UserProfile`), including public bio, avatar URL, time zone, and verification flag.
- Default account bootstrapping (initial wallet and free subscription initialization during registration).

---

## 3. Responsibilities
- Implement and maintain `POST /api/auth/register` and `POST /api/auth/login`.
- Implement `GET /api/profiles/me`, `PUT /api/profiles/me`, and `GET /api/profiles/{id}`.
- Ensure all JWT tokens embed correct standard claims (`sub` as user Guid, `email`, `jti`, and `role`).
- Maintain `CurrentUserService` to reliably extract `UserId` from `ClaimsPrincipal`.
- Coordinate user creation so that every new user receives a `Wallet` (0 Available, 0 Held) and an active `UserSubscription` (Free plan).
- Write comprehensive unit and integration tests covering authentication, token validation, and profile CRUD.

---

## 4. Domain Entities Owned

| Entity | Primary Key | Table | Description |
|---|---|---|---|
| `ApplicationUser` | `Guid` | `Users` | Core identity record extending `IdentityUser<Guid>`. Includes soft-delete flags. |
| `UserProfile` | `Guid` | `UserProfiles` | Extended 1:1 profile record linked to `ApplicationUser`. Holds display name, bio, photo, timezone. |

---

## 5. Application Services Owned

| Interface / Class | Location | Status | Description |
|---|---|---|---|
| `IJwtTokenService` | `src/SkillSwap.Application/Abstractions/IJwtTokenService.cs` | **IMPLEMENTED** | Generates signed JWT access tokens with user claims. |
| `JwtTokenService` | `src/SkillSwap.Infrastructure/Services/JwtTokenService.cs` | **IMPLEMENTED** | Infrastructure implementation using `JwtSettings`. |
| `ICurrentUserService` | `src/SkillSwap.Application/Abstractions/ICurrentUserService.cs` | **IMPLEMENTED** | Provides current authenticated `UserId`, `Email`, and roles. |
| `CurrentUserService` | `src/SkillSwap.Infrastructure/Services/CurrentUserService.cs` | **IMPLEMENTED** | Extracts claims from `IHttpContextAccessor`. |
| `IAuthService` | `src/SkillSwap.Application/Abstractions/IAuthService.cs` | **PLANNED** | Application contract for register, login, and password reset. |
| `IProfileService` | `src/SkillSwap.Application/Abstractions/IProfileService.cs` | **PLANNED** | Application contract for querying and updating user profiles. |

---

## 6. Controllers / API Areas Owned

| Controller | Route Area | Status | Description |
|---|---|---|---|
| `AuthController` | `/api/auth` | **PLANNED** | Public authentication endpoints (register, login). |
| `ProfilesController` | `/api/profiles` | **PLANNED** | Authenticated and public profile endpoints. |

---

## 7. Files I Own

### Entities & Database Configurations:
- `src/SkillSwap.Infrastructure/Identity/ApplicationUser.cs`
- `src/SkillSwap.Domain/Entities/UserProfile.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/UserProfileConfiguration.cs`

### DTOs:
- `src/SkillSwap.Application/DTOs/Auth/RegisterRequestDto.cs`
- `src/SkillSwap.Application/DTOs/Auth/LoginRequestDto.cs`
- `src/SkillSwap.Application/DTOs/Auth/AuthResponseDto.cs`
- Planned: `src/SkillSwap.Application/DTOs/Profiles/UserProfileDto.cs`
- Planned: `src/SkillSwap.Application/DTOs/Profiles/UpdateProfileRequestDto.cs`

### Infrastructure & Services:
- `src/SkillSwap.Infrastructure/Services/JwtTokenService.cs`
- `src/SkillSwap.Infrastructure/Services/JwtSettings.cs`
- `src/SkillSwap.Infrastructure/Services/CurrentUserService.cs`
- `src/SkillSwap.API/Extensions/AuthenticationExtensions.cs`

### Controllers (To be implemented):
- `src/SkillSwap.API/Controllers/AuthController.cs` (PLANNED)
- `src/SkillSwap.API/Controllers/ProfilesController.cs` (PLANNED)

---

## 8. Files I Must Not Modify Without Coordination

- `src/SkillSwap.Infrastructure/Persistence/ApplicationDbContext.cs` (Owner: Member 4)
- `src/SkillSwap.Infrastructure/Migrations/*` (Owner: Member 4 ONLY)
- `src/SkillSwap.API/Program.cs` (Coordinate with Member 4 / Team Lead)
- `src/SkillSwap.Infrastructure/DependencyInjection.cs` (Shared registration; edit only Auth section)
- `src/SkillSwap.Application/Services/WalletService.cs` (Owner: Member 4)
- `src/SkillSwap.Application/Services/SessionBookingService.cs` (Owner: Member 4)

---

## 9. Dependencies
- **Downstream Consumer of:**
  - ASP.NET Core Identity & Microsoft.AspNetCore.Authentication.JwtBearer.
- **Provides Services to:**
  - All 4 other members via `ICurrentUserService` (`UserId`, `Email`, `IsInRole`).
  - Member 4 and Member 5 during user registration (wallet provisioning and free subscription setup).

---

## 10. Cross-Module Contracts

1. **User Identity Invariant:**
   - User primary key is strictly `Guid`.
   - All modules anchor user records to this `Guid`.
2. **Registration Bootstrapping Contract:**
   - When a user successfully registers:
     1. Insert `ApplicationUser` via `UserManager<ApplicationUser>`.
     2. Insert corresponding `UserProfile` (`DisplayName = request.DisplayName`).
     3. Insert corresponding `Wallet` (`AvailableMinutes = 0`, `HeldMinutes = 0`).
     4. Insert corresponding `UserSubscription` (`PlanId = FreePlanId`, `Status = Active`).
   - All 4 records must be committed together.
3. **Claims Structure:**
   - `sub`: String representation of `Guid` (`user.Id.ToString()`).
   - `email`: User's normalized email.
   - `role`: Assigned application roles (e.g. `"User"`, `"Admin"`).

---

## 11. Business Rules I Must Respect

1. **Email Uniqueness:** Email is unique and normalized. Registration with an existing email returns 400 Bad Request.
2. **Password Policy:** Enforce minimum 8 characters, at least 1 digit, 1 uppercase, 1 lowercase, and 1 non-alphanumeric character.
3. **Lockout Policy:** 5 consecutive failed access attempts triggers a 15-minute account lockout.
4. **Calculated Profile Statistics:** Ratings and review counts **must not be stored** on `UserProfile`. They must be calculated dynamically from `Reviews` (owned by Member 5).
5. **Soft Deletes:** Account deactivation sets `IsDeleted = true` and `DeletedAtUtc = DateTime.UtcNow`. Never physically delete user accounts.

---

## 12. Planned Endpoints

| Method | Route | Description | Auth Required | Role |
|---|---|---|---|---|
| `POST` | `/api/auth/register` | Register new user account, profile, wallet, and free plan | No | Anonymous |
| `POST` | `/api/auth/login` | Authenticate with email/password and receive JWT token | No | Anonymous |
| `GET` | `/api/profiles/me` | Retrieve profile of the currently authenticated user | Yes | Authenticated |
| `PUT` | `/api/profiles/me` | Update profile details (Bio, PhotoUrl, TimeZoneId, DisplayName) | Yes | Authenticated |
| `GET` | `/api/profiles/{id}` | Retrieve public profile of any user | Yes | Authenticated |

---

## 13. Existing Implemented Endpoints
*None currently in API controllers. Health probe (`/api/health`) is shared infrastructure.*

---

## 14. Events / SignalR / Notifications
- Emits user registration event to trigger welcome notification (to be handled by Member 5).
- Ensures JWT Bearer authentication is compatible with SignalR query string token passing (`access_token` query param in `/hubs/chat`).

---

## 15. Database Responsibility
- Tables: `Users`, `UserProfiles`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `RoleClaims`, `UserTokens`.
- Any modification to `UserProfile` properties must be reflected in `UserProfileConfiguration.cs` and coordinated with Member 4 for migration generation.

---

## 16. Validation & Authorization
- `RegisterRequestDto`: Mandatory Email (valid email format), Password (matching complexity), DisplayName (1-100 chars).
- `LoginRequestDto`: Mandatory Email and Password.
- `UpdateProfileRequestDto`: DisplayName (max 100), Bio (max 1000), PhotoUrl (valid URI or null, max 500), TimeZoneId (valid IANA/Windows timezone).
- `[Authorize]` must guard all profile endpoints except public read `/api/profiles/{id}`.

---

## 17. Testing Requirements
- **Unit Tests:**
  - Token generation produces expected claims and expiration.
  - `CurrentUserService` correctly resolves user Guid and claims.
  - Registration validator flags weak passwords and invalid emails.
- **Integration Tests:**
  - Registration successfully creates User, Profile, Wallet, and Subscription.
  - Duplicate registration returns 400.
  - Login with valid credentials returns 200 with JWT token.
  - Login with invalid password returns 400/401 and increments access failed count.

---

## 18. Definition of Done
- [ ] DTOs created with complete validation rules.
- [ ] Identity registration and login fully operational.
- [ ] JWT tokens issued and validated successfully by API middleware.
- [ ] Profile CRUD operational with proper user isolation.
- [ ] Unit and integration tests written and passing 100%.
- [ ] Zero changes made to EF migrations or financial services.
- [ ] PR created against `develop` and approved by Member 4 and one other reviewer.

---

## 19. PR Checklist
- [ ] Does `dotnet test` pass with 0 failures?
- [ ] Are password requirements enforced?
- [ ] Are sensitive tokens excluded from logs?
- [ ] Is `UserProfile` updated without mutating `Wallet` or `Session`?
- [ ] Does the branch follow `feature/auth-profile` naming?

---

## 20. Common Mistakes to Avoid
- **Mistake 1:** Storing calculated rating values directly inside `UserProfile`. (Ratings belong to dynamic queries over `Reviews`).
- **Mistake 2:** Hardcoding JWT secret keys or expiration times in code. (Always bind to `JwtSettings` from configuration).
- **Mistake 3:** Attempting to generate an EF Core migration independently. (Coordinate with Member 4).
- **Mistake 4:** Forgetting to seed/initialize a `Wallet` and `UserSubscription` upon user registration.

---
*Member 1 Ownership Guide -- SkillSwap Backend Team*
