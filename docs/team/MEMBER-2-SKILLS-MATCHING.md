# Member 2 -- Skills, Availability & Matching

## 1. Mission
Own the skill taxonomy, category hierarchy, user skill profiles (teach and learn capabilities), recurring weekly availability schedules, and the computed matching algorithm that powers skill discovery and peer recommendations across SkillSwap.

---

## 2. Scope of Ownership
- Master skill catalog and category organization (`Category`, `Skill`).
- User skill profiling (`UserSkill` with `Type = Teach` or `Type = Learn` and proficiency level).
- Recurring weekly availability schedules (`AvailabilitySlot`).
- Skill catalog search, auto-completion, and category filtering.
- Computed matching algorithm that connects users who want to learn what another user can teach.
- Invariant: Matching is **strictly computed on-demand**; there is **NO persisted Match entity**.

---

## 3. Responsibilities
- Implement category discovery APIs (`GET /api/categories`).
- Implement skill search and lookup APIs (`GET /api/skills`, `GET /api/skills/{id}`).
- Implement user skill management APIs (`GET /api/users/me/skills`, `POST /api/users/me/skills`, `DELETE /api/users/me/skills/{id}`).
- Implement user availability schedule APIs (`GET /api/users/me/availability`, `PUT /api/users/me/availability`).
- Implement computed matching endpoint (`GET /api/matches`) returning scored user recommendations.
- Provide fast query abstractions for checking whether a user actively teaches a given skill.
- Write unit tests covering matching heuristics, availability validations, and search filters.

---

## 4. Domain Entities Owned

| Entity | Primary Key | Table | Description |
|---|---|---|---|
| `Category` | `int` | `Categories` | High-level skill categories (e.g., Programming, Languages, Music). |
| `Skill` | `int` | `Skills` | Specific skill catalog entry under a category. |
| `UserSkill` | `long` | `UserSkills` | Associative record indicating if a user teaches or wants to learn a skill. |
| `AvailabilitySlot` | `long` | `AvailabilitySlots` | Weekly recurring availability window for a user (DayOfWeek, StartTime, EndTime). |

### Enums Owned:
- `UserSkillType` (`Teach = 1`, `Learn = 2` with legacy aliases `Teaching`, `Learning`).
- `UserSkillLevel` (`Beginner = 1`, `Intermediate = 2`, `Advanced = 3`).

---

## 5. Application Services Owned

| Interface / Class | Location | Status | Description |
|---|---|---|---|
| `ICategoryService` | `src/SkillSwap.Application/Abstractions/ICategoryService.cs` | **PLANNED** | Queries and manages skill categories. |
| `ISkillService` | `src/SkillSwap.Application/Abstractions/ISkillService.cs` | **PLANNED** | Queries skills catalog and handles user skill mappings. |
| `IAvailabilityService` | `src/SkillSwap.Application/Abstractions/IAvailabilityService.cs` | **PLANNED** | Manages weekly availability slots and slot collision validation. |
| `IMatchingService` | `src/SkillSwap.Application/Abstractions/IMatchingService.cs` | **PLANNED** | Evaluates computed matching logic between teachers and learners. |

---

## 6. Controllers / API Areas Owned

| Controller | Route Area | Status | Description |
|---|---|---|---|
| `CategoriesController` | `/api/categories` | **PLANNED** | Read categories and admin category creation. |
| `SkillsController` | `/api/skills` | **PLANNED** | Search skills catalog, query by category, admin skill creation. |
| `UserSkillsController` | `/api/users/me/skills` | **PLANNED** | Manage current user's teach and learn skill lists. |
| `AvailabilityController` | `/api/users/me/availability` | **PLANNED** | Manage current user's weekly recurring availability. |
| `MatchesController` | `/api/matches` | **PLANNED** | Computed recommendations matching user's learn/teach profile. |

---

## 7. Files I Own

### Entities & Configurations:
- `src/SkillSwap.Domain/Entities/Category.cs`
- `src/SkillSwap.Domain/Entities/Skill.cs`
- `src/SkillSwap.Domain/Entities/UserSkill.cs`
- `src/SkillSwap.Domain/Entities/AvailabilitySlot.cs`
- `src/SkillSwap.Domain/Enums/UserSkillType.cs`
- `src/SkillSwap.Domain/Enums/UserSkillLevel.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/SkillConfiguration.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/UserSkillConfiguration.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/AvailabilitySlotConfiguration.cs`

### Planned Application DTOs:
- `src/SkillSwap.Application/DTOs/Skills/CategoryDto.cs`
- `src/SkillSwap.Application/DTOs/Skills/SkillDto.cs`
- `src/SkillSwap.Application/DTOs/Skills/UserSkillDto.cs`
- `src/SkillSwap.Application/DTOs/Skills/AddUserSkillRequest.cs`
- `src/SkillSwap.Application/DTOs/Availability/AvailabilitySlotDto.cs`
- `src/SkillSwap.Application/DTOs/Availability/UpdateAvailabilityRequest.cs`
- `src/SkillSwap.Application/DTOs/Matching/MatchRecommendationDto.cs`

---

## 8. Files I Must Not Modify Without Coordination

- `src/SkillSwap.Domain/Entities/SwapRequest.cs` (Owner: Member 3)
- `src/SkillSwap.Domain/Entities/Session.cs` (Owner: Member 4)
- `src/SkillSwap.Domain/Entities/Wallet.cs` (Owner: Member 4)
- `src/SkillSwap.Infrastructure/Persistence/ApplicationDbContext.cs` (Owner: Member 4)
- `src/SkillSwap.Infrastructure/Migrations/*` (Owner: Member 4 ONLY)
- `src/SkillSwap.API/Program.cs` (Team Lead / Member 4)

---

## 9. Dependencies
- **Consumes:** `ICurrentUserService` (Member 1) to identify caller.
- **Supplies to:**
  - Member 3 (`SwapRequest` creation validates that requested `SkillId` exists).
  - Member 4 (`SessionBookingService` validates that `ReceiverId` actively teaches `SkillId`).

---

## 10. Cross-Module Contracts

1. **Teacher Qualification Invariant:**
   - A user can only receive a swap request or be booked for a session if they possess an active `UserSkill` record where:
     ```csharp
     userSkill.SkillId == requestedSkillId && userSkill.Type == UserSkillType.Teach
     ```
2. **Computed Matching Contract:**
   - Matching is calculated dynamically by joining:
     - User A's `Learn` skills matching User B's `Teach` skills.
     - Optional two-way mutual exchange scoring (User B's `Learn` skills matching User A's `Teach` skills).
     - Availability slot overlap between User A and User B.
   - **No persistent `Match` table or entity may be created.**
3. **Skill Foreign Keys:**
   - `SkillId` (`int`) is the foreign key on `SwapRequest` and `Session`. Foreign key cascade is strictly `DeleteBehavior.Restrict`.

---

## 11. Business Rules I Must Respect

1. **Category & Skill Uniqueness:**
   - `Category.Name` is unique.
   - `Skill.Name` is unique within each `CategoryId` (`IX_Skills_CategoryId_Name`).
2. **UserSkill Duplication:**
   - A user cannot add the exact same skill with the same `UserSkillType` twice.
3. **Availability Slot Rules:**
   - `StartTime` must be strictly earlier than `EndTime`.
   - Availability slots for the same user on the same `DayOfWeek` must not overlap.
   - Stored using SQL Server `time` via .NET `TimeOnly`.
4. **Active Flags:**
   - Soft toggle via `IsActive` on Category and Skill. Inactive skills cannot be newly selected by users.

---

## 12. Planned Endpoints

| Method | Route | Description | Auth Required | Role |
|---|---|---|---|---|
| `GET` | `/api/categories` | List all active categories | No | Anonymous |
| `GET` | `/api/skills` | Search/filter skills catalog | No | Anonymous |
| `GET` | `/api/skills/{id}` | Get skill detail by ID | No | Anonymous |
| `POST` | `/api/skills` | Create new catalog skill | Yes | Admin |
| `GET` | `/api/users/me/skills` | List authenticated user's skills | Yes | Authenticated |
| `POST` | `/api/users/me/skills` | Add skill to teach or learn list | Yes | Authenticated |
| `DELETE` | `/api/users/me/skills/{id}` | Remove skill from user profile | Yes | Authenticated |
| `GET` | `/api/users/me/availability` | Retrieve weekly recurring availability | Yes | Authenticated |
| `PUT` | `/api/users/me/availability` | Replace weekly recurring availability slots | Yes | Authenticated |
| `GET` | `/api/matches` | Retrieve computed peer match recommendations | Yes | Authenticated |

---

## 13. Existing Implemented Endpoints
*None currently in API controllers.*

---

## 14. Events / SignalR / Notifications
*Not applicable for core catalog and matching endpoints.*

---

## 15. Database Responsibility
- Tables: `Categories`, `Skills`, `UserSkills`, `AvailabilitySlots`.
- Ensure indexes:
  - `IX_Skills_CategoryId_Name` (Unique)
  - `IX_UserSkills_UserId_SkillId_Type` (Unique)
  - `IX_AvailabilitySlots_UserId_DayOfWeek`
- Coordinate all migration generation through Member 4.

---

## 16. Validation & Authorization
- `AddUserSkillRequest`: Valid `SkillId > 0`, valid `UserSkillType` enum (Teach/Learn), valid `UserSkillLevel` enum.
- `UpdateAvailabilityRequest`: Slots array must not contain inverted times (`StartTime >= EndTime`) or self-overlapping time spans on the same day.
- Admin protection on `POST /api/skills` (`[Authorize(Roles = "Admin")]`).

---

## 17. Testing Requirements
- **Unit Tests:**
  - Skill search correctly filters by search term and category.
  - User cannot add duplicate `(UserId, SkillId, Type)`.
  - Availability validation rejects overlapping slots.
  - Computed matching scoring logic prioritizes two-way mutual matches.
- **Integration Tests:**
  - Querying matches against in-memory/test database returns correct candidate users.
  - Non-admin blocked from creating skills.

---

## 18. Definition of Done
- [ ] Category and Skill search APIs implemented with pagination.
- [ ] UserSkill teach/learn profile endpoints tested and verified.
- [ ] Availability slot validation thoroughly covers all overlap edge cases.
- [ ] Dynamic matching query executes efficiently without introducing a persistent Match entity.
- [ ] 100% test pass rate on `dotnet test`.
- [ ] PR reviewed and approved by Member 4 and one other member.

---

## 19. PR Checklist
- [ ] Does `dotnet test` pass with 0 errors?
- [ ] Are all database queries avoiding N+1 performance bottlenecks?
- [ ] Is matching computed dynamically without creating a database table?
- [ ] Are availability times stored as UTC or timezone-neutral TimeOnly?
- [ ] Does the branch follow `feature/skills-matching`?

---

## 20. Common Mistakes to Avoid
- **Mistake 1:** Creating a persistent `Match` entity or table in the database. (Matching is strictly dynamic/computed).
- **Mistake 2:** Allowing `StartTime >= EndTime` in availability slots.
- **Mistake 3:** Omitting the check that a teacher actually offers `Type = UserSkillType.Teach` for a skill.
- **Mistake 4:** Generating EF migrations directly instead of routing through Member 4.

---
*Member 2 Ownership Guide -- SkillSwap Backend Team*
