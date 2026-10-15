# Developer 3 — Skills, Availability & Matching

**Developer:** [Mario Wael](https://github.com/mariowael) (@mariowael)  
**Role:** Developer 3  
**Module:** Skills Catalog, User Capabilities, Availability Schedules & Matching Engine  

---

## 1. Mission

Own the skill taxonomy, category hierarchy, user skill profiles (teach and learn capabilities), recurring weekly availability schedules, and the dynamic computed matching algorithm that powers skill discovery and peer recommendations across SkillSwap.

---

## 2. Scope of Ownership

- Master skill catalog and category organization (`Category`, `Skill`).
- User skill profiling (`UserSkill` with `Type = Teach` or `Type = Learn` and proficiency level).
- Recurring weekly availability schedules (`AvailabilitySlot`).
- Skill catalog search, auto-completion, and category filtering.
- Dynamic computed matching algorithm that connects users who want to learn what another user can teach.
- **Architectural Invariant:** Matching is **strictly computed on-demand**; there is **NO persisted Match entity**.

---

## 3. Responsibilities

- Maintain category discovery APIs (`GET /api/categories`, `POST /api/categories`).
- Maintain skill search and lookup APIs (`GET /api/skills`, `GET /api/skills/{id}`, `POST /api/skills`).
- Maintain user skill management APIs (`GET /api/user-skills/me`, `POST /api/user-skills`, `DELETE /api/user-skills/{id}`, `GET /api/user-skills/user/{userId}`).
- Maintain user availability schedule APIs (`GET /api/availability/me`, `PUT /api/availability/me`, `GET /api/availability/user/{userId}`).
- Maintain computed matching endpoint (`GET /api/matches`) returning scored user recommendations based on complementary teach/learn skills and availability overlaps.
- Provide fast query abstractions for checking whether a user actively teaches a given skill.
- Write and maintain comprehensive unit tests covering matching heuristics, availability validations, and search filters.

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
| `ICategoryService` | `src/SkillSwap.Application/Abstractions/ICategoryService.cs` | **IMPLEMENTED** | Queries and manages skill categories. |
| `CategoryService` | `src/SkillSwap.Application/Services/CategoryService.cs` | **IMPLEMENTED** | Implementation of category listing and administration. |
| `ISkillService` | `src/SkillSwap.Application/Abstractions/ISkillService.cs` | **IMPLEMENTED** | Queries skills catalog and handles user skill mappings. |
| `SkillService` | `src/SkillSwap.Application/Services/SkillService.cs` | **IMPLEMENTED** | Skill catalog search, auto-completion, and user skill management. |
| `IAvailabilityService` | `src/SkillSwap.Application/Abstractions/IAvailabilityService.cs` | **IMPLEMENTED** | Manages weekly availability slots and slot collision validation. |
| `AvailabilityService` | `src/SkillSwap.Application/Services/AvailabilityService.cs` | **IMPLEMENTED** | Slot validation, day-of-week checks, and availability schedules. |
| `IMatchingService` | `src/SkillSwap.Application/Abstractions/IMatchingService.cs` | **IMPLEMENTED** | Evaluates computed matching logic between teachers and learners. |
| `MatchingService` | `src/SkillSwap.Application/Services/MatchingService.cs` | **IMPLEMENTED** | Scoring algorithm combining teach/learn overlap and availability match. |

---

## 6. Controllers & Endpoints Owned

| Controller | Route Area | Method & Endpoint | Description |
|---|---|---|---|
| `CategoriesController` | `/api/categories` | `GET /api/categories` | Retrieve all active skill categories. |
| `CategoriesController` | `/api/categories` | `POST /api/categories` | Create new category (Admin). |
| `SkillsController` | `/api/skills` | `GET /api/skills` | Search skills catalog with optional category and search filters. |
| `SkillsController` | `/api/skills` | `GET /api/skills/{id}` | Retrieve skill details by ID. |
| `SkillsController` | `/api/skills` | `POST /api/skills` | Create new skill entry (Admin). |
| `UserSkillsController` | `/api/user-skills` | `GET /api/user-skills/me` | List current user's declared teach and learn skills. |
| `UserSkillsController` | `/api/user-skills` | `POST /api/user-skills` | Declare a skill to teach or learn with proficiency level. |
| `UserSkillsController` | `/api/user-skills` | `DELETE /api/user-skills/{id}` | Remove a declared user skill. |
| `UserSkillsController` | `/api/user-skills` | `GET /api/user-skills/user/{userId}` | List public skills declared by a user. |
| `AvailabilityController` | `/api/availability` | `GET /api/availability/me` | Retrieve current user's weekly recurring availability. |
| `AvailabilityController` | `/api/availability` | `PUT /api/availability/me` | Replace weekly recurring availability schedule. |
| `AvailabilityController` | `/api/availability` | `GET /api/availability/user/{userId}` | Retrieve public availability schedule for a user. |
| `MatchesController` | `/api/matches` | `GET /api/matches` | Retrieve scored matching recommendations for current user. |

---

## 7. Files Owned

### Entities & Configurations
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

### DTOs
- `src/SkillSwap.Application/DTOs/Skills/*`
- `src/SkillSwap.Application/DTOs/Availability/*`
- `src/SkillSwap.Application/DTOs/Matching/*`

### Services
- `src/SkillSwap.Application/Abstractions/ICategoryService.cs`
- `src/SkillSwap.Application/Abstractions/ISkillService.cs`
- `src/SkillSwap.Application/Abstractions/IAvailabilityService.cs`
- `src/SkillSwap.Application/Abstractions/IMatchingService.cs`
- `src/SkillSwap.Application/Services/CategoryService.cs`
- `src/SkillSwap.Application/Services/SkillService.cs`
- `src/SkillSwap.Application/Services/AvailabilityService.cs`
- `src/SkillSwap.Application/Services/MatchingService.cs`

### Controllers
- `src/SkillSwap.API/Controllers/CategoriesController.cs`
- `src/SkillSwap.API/Controllers/SkillsController.cs`
- `src/SkillSwap.API/Controllers/UserSkillsController.cs`
- `src/SkillSwap.API/Controllers/AvailabilityController.cs`
- `src/SkillSwap.API/Controllers/MatchesController.cs`

### Test Suites
- `tests/SkillSwap.Tests/Unit/CategoriesControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/CategoryServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/SkillsControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/SkillServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/UserSkillsControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/AvailabilityControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/AvailabilityServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/MatchesControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/MatchingServiceTests.cs`

---

## 8. Cross-Module Contracts & Dependencies

1. **Supplies Skill Verification to Developer 1:**
   - When a swap request is initiated, Developer 1 verifies that `SwapRequest.ReceiverId` actively teaches `SwapRequest.SkillId` via `UserSkills`.
2. **Supplies Availability Overlap to Session Scheduling:**
   - Teacher availability windows define valid session booking ranges for Developer 1's session engine.
3. **Consumes User Blocks from Developer 2:**
   - Matching suggestions exclude users who have blocked or been blocked by the searching user.
4. **Coordination with Developer 1:**
   - All schema changes must be submitted to Developer 1 as EF Core configurations.

---
*Developer 3 Ownership Guide — SkillSwap Backend Engineering Team*
