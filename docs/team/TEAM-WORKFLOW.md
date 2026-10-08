# SkillSwap Backend -- Team Workflow & Engineering Guidelines

This document outlines the collaborative engineering workflow, Git branching standards, pull request policies, database migration gates, and conflict prevention rules for the 5-member backend development team.

---

## Table of Contents
1. [Git Branching Strategy](#1-git-branching-strategy)
2. [Commit Conventions](#2-commit-conventions)
3. [Pull Request (PR) Lifecycle & Process](#3-pull-request-pr-lifecycle--process)
4. [Code Review Rules & SLA](#4-code-review-rules--sla)
5. [Shared File & Protected Code Policy](#5-shared-file--protected-code-policy)
6. [Database Migration Policy & Protocol](#6-database-migration-policy--protocol)
7. [Merge Conflict Resolution Policy](#7-merge-conflict-resolution-policy)
8. [Testing Policy & Quality Standards](#8-testing-policy--quality-standards)
9. [API Contract Change Policy](#9-api-contract-change-policy)
10. [Definition of Done (DoD)](#10-definition-of-done-dod)
11. [Official Project Execution Roadmap & Integration Sequence](#11-official-project-execution-roadmap--integration-sequence)
12. [Emergency & Hotfix Policy](#12-emergency--hotfix-policy)
13. [The 12 Conflict-Free Operational Rules](#13-the-12-conflict-free-operational-rules)

---

## 1. Git Branching Strategy

The repository follows a modified GitFlow branching model centered on two perpetual branches and dedicated feature branches:

```
main (Production / Locked / Tagged Releases)
  ^
  |  (Merge via release PR only)
develop (Integration Branch / CI Gate)
  ^
  +-- feature/auth-profile        (Phase 4 / Member 1)
  +-- feature/skills-matching     (Phase 5 / Member 2)
  +-- feature/swap-chat           (Phase 6 / Member 3)
  +-- feature/session-wallet      (Phase 3 Maintenance / Member 4)
  +-- feature/reviews-platform    (Phase 7 / Member 5)
```

### Branch Invariants:
1. **`main`**:
   - Represents stable, production-ready releases.
   - **Direct pushes are strictly blocked.**
   - Only receives merges from `develop` following comprehensive smoke and integration testing.
2. **`develop`**:
   - Primary shared integration branch.
   - All feature branches branch off `develop` and merge back into `develop`.
   - **Direct pushes are blocked.** Merges occur solely through approved Pull Requests that pass CI tests.
3. **Dedicated Feature Branches:**
   - `feature/auth-profile`: Member 1 (Auth, Identity, UserProfile).
   - `feature/skills-matching`: Member 2 (Categories, Skills, UserSkills, Availability, Matching).
   - `feature/swap-chat`: Member 3 (SwapRequests, Conversations, Messages, SignalR ChatHub).
   - `feature/session-wallet`: Member 4 (Sessions, Wallets, CreditTransactions, Background Workers).
   - `feature/reviews-platform`: Member 5 (Reviews, Notifications, Badges, Favorites, Blocks, Reports, Subscriptions).
   - Sub-features may use format: `feature/<module>/<short-description>` (e.g., `feature/auth/register-endpoint`).

---

## 2. Commit Conventions

The team strictly adheres to the **Conventional Commits** specification. Every commit message must be structured as follows:

```
<type>(<scope>): <short imperative summary>

[optional detailed body explaining rationale]

[optional footer referencing issue or PR]
```

### Allowed Types:
- `feat`: A new user-facing or API feature.
- `fix`: A bug fix or invariant patch.
- `docs`: Documentation changes only (e.g., in `docs/team/`).
- `refactor`: Code change that neither fixes a bug nor adds a feature.
- `test`: Adding or correcting unit/integration tests.
- `chore`: Tooling, build scripts, or dependency configuration.

### Allowed Scopes:
- `auth`, `profile`, `skills`, `matching`, `swap`, `chat`, `session`, `wallet`, `ledger`, `reviews`, `notifications`, `infra`, `db`.

### Examples:
- `feat(auth): implement user registration endpoint and DTO validation`
- `fix(wallet): correct deterministic lock ordering for concurrent bookings`
- `test(session): add dispute resolution authorization tests`
- `docs(team): update cross-module contract for swap request accepted state`

---

## 3. Pull Request (PR) Lifecycle & Process

### Step 1: Branch Preparation
Before creating a PR, the developer must synchronize with `develop`:
```bash
git checkout develop
git pull origin develop
git checkout feature/your-feature
git rebase develop
dotnet test
```

### Step 2: PR Creation
- Target branch must always be **`develop`**.
- PR Title must follow conventional format: `feat(skills): add user availability management endpoints`.
- PR Description must detail:
  1. What changes were made.
  2. Which files were touched.
  3. Tests executed and confirmation that `dotnet test` passed 100%.
  4. Any shared contract or database impact.

### Step 3: CI Quality Gate
Every PR must pass automated CI pipeline checks:
- Clean build of `SkillSwap.sln` with zero compilation errors and warnings treated as errors.
- 100% passing xUnit tests across all test suites.

---

## 4. Code Review Rules & SLA

1. **Reviewer Quorum:**
   - Every PR requires **at least 2 approvals** before merging.
   - Any PR touching `src/SkillSwap.Domain/`, `ApplicationDbContext.cs`, or migrations requires explicit approval from **Member 4 (Lead)**.
2. **Review SLA:**
   - Team members must review open PRs within **24 hours** of submission.
3. **Review Focus:**
   - Respect for Clean Architecture boundaries (`SkillSwap.Application` must never reference `Infrastructure` or `API`).
   - Correctness of business invariants (non-negative balances, UTC timestamps, role contracts).
   - Absence of unrelated whitespace/drive-by refactoring changes.

---

## 5. Shared File & Protected Code Policy

Certain files are shared across multiple modules and represent high risk for merge conflicts and regressions:

1. **`src/SkillSwap.API/Program.cs`:**
   - Only add scoped service registrations or route mappings in the designated section.
   - Never reorder existing middleware (`GlobalExceptionHandlingMiddleware`, `UseAuthentication`, `UseAuthorization`, `UseCors`).
2. **`src/SkillSwap.Infrastructure/DependencyInjection.cs`:**
   - Add module dependencies within your designated module comment block.
3. **`src/SkillSwap.Infrastructure/Persistence/ApplicationDbContext.cs`:**
   - Do not directly edit `OnModelCreating` to add inline fluent configs. All entity mappings must reside in dedicated `IEntityTypeConfiguration<T>` files under `Configurations/`.
4. **`src/SkillSwap.Domain/Enums/*`:**
   - Never remove or renumber enum integer values. Doing so corrupts persisted database records.

---

## 6. Database Migration Policy & Protocol

> **CRITICAL DIRECTIVE:**
> **Member 4 is the SOLE authority for EF Core migrations.**
> No other team member may execute `dotnet ef migrations add` or modify migration files.

### 5-Step Protocol for Schema Changes:
1. **Developer modifies Domain Entity:** The owning developer adds/modifies fields on their entity under `src/SkillSwap.Domain/Entities/`.
2. **Developer creates Entity Configuration:** The owning developer creates or updates `IEntityTypeConfiguration<T>` in `src/SkillSwap.Infrastructure/Persistence/Configurations/`.
3. **Developer creates RFC / Coordination Issue:** The developer notifies Member 4 detailing the requested schema modification and index requirements.
4. **Member 4 Generates Migration:** Member 4 inspects the configuration, ensures foreign keys use `DeleteBehavior.Restrict`, and generates the migration:
   ```bash
   dotnet ef migrations add <DescriptiveName> --project src/SkillSwap.Infrastructure --startup-project src/SkillSwap.API
   ```
5. **Member 4 Validates & Commits:** Member 4 executes database migration tests and merges the migration directly into `develop`.

---

## 7. Merge Conflict Resolution Policy

If a merge conflict occurs when rebasing on `develop`:
1. **Never guess another member's business logic.**
2. Consult the relevant member's specification document (`MEMBER-X-*.md`).
3. If the conflict involves shared DTOs or services, conduct an immediate synchronous pairing session with the other owner.
4. After resolving conflicts locally, re-run `dotnet build` and `dotnet test`.
5. Never use `git push --force` on shared branches. Use `git push --force-with-lease` on personal feature branches only.

---

## 8. Testing Policy & Quality Standards

1. **Zero Test Regressions:**
   - Under no circumstances will a PR be merged if any existing test fails.
   - The test suite currently contains **74 passing tests**. Every PR must maintain or increase this count.
2. **Mandatory Test Coverage:**
   - **Unit Tests:** Every new application service method must have unit tests covering success paths, edge cases, and expected exception throws.
   - **Authorization Tests:** Every endpoint must test that unauthorized callers receive 401 or 403.
   - **Invariant Tests:** Business rules (such as non-negative wallets or 30/60/90 duration limits) must have explicit assertions.
3. **Execution Command:**
   ```bash
   dotnet test D:\SkillSwap\SkillSwap.sln --logger "console;verbosity=normal"
   ```

---

## 9. API Contract Change Policy

1. **Backward Compatibility:**
   - Existing endpoints and DTO properties must not be renamed or deleted once merged into `develop`.
   - New properties on request DTOs must be optional (`nullable`) or provide sensible default values.
2. **Route Conventions:**
   - Resource-oriented URLs (`/api/categories`, `/api/sessions/{id}`).
   - Plural nouns for collections.
   - Verbs used only for state-changing operations (`/confirm`, `/cancel`, `/complete`, `/no-show`, `/dispute`).
3. **Response Wrappers:**
   - Application services return `Result<T>` or `PagedResult<T>` from `SkillSwap.Application.Common`.
   - Controllers unpack results into standard HTTP status codes (200 OK, 201 Created, 400 BadRequest, 404 NotFound).

---

## 10. Definition of Done (DoD)

A task or PR is considered **Done** ONLY when:
- [ ] Clean Architecture layer boundaries are strictly maintained:
      - `SkillSwap.Application` MUST NOT depend on `SkillSwap.Infrastructure` or `SkillSwap.API`.
      - `SkillSwap.Application` owns application contracts/abstractions used by Infrastructure implementations.
      - `SkillSwap.Domain` contains core domain models and business concepts.
      - `Infrastructure` implements persistence, Identity, SQL Server, and background services.
      - `API` delegates all business behavior to Application services.
- [ ] Domain entities (22 total) and enums comply with `DATABASE.md` and `Business-Rules.md`.
- [ ] Request DTOs include comprehensive input validation.
- [ ] Authorization checks are enforced at controller and application service levels.
- [ ] Invariant business rules (e.g., non-negative balances, role contracts) are verified.
- [ ] Unit tests are written and passing for all new logic.
- [ ] Integration tests are updated where applicable.
- [ ] `dotnet test` executes with 100% pass rate.
- [ ] No unrelated files or formatting changes are included in the diff.
- [ ] Swagger/OpenAPI annotations (`[ProducesResponseType]`) are added to all controller endpoints.
- [ ] Documentation is updated in `docs/team/` if any contract was extended.
- [ ] PR is reviewed and approved by at least 2 team members.

---

## 11. Official Project Execution Roadmap & Integration Sequence

The team follows the official 9-phase project execution roadmap:

| Phase | Milestone Name | Primary Lead | Status | Parallel Execution Notes |
|---|---|---|---|---|
| **Phase 1** | **Foundation** | Architecture Agent | ✅ **COMPLETE** | Core solution, projects, DI, health check |
| **Phase 2** | **Database & Domain** | Domain Agent | ✅ **COMPLETE** | 22 entities, 12 enums, EF configurations, initial migration |
| **Phase 3** | **Core Business Engine** | Member 4 / Financial Lead | ✅ **COMPLETE** | Wallet, ledger, booking engine, locking, 74 tests |
| **Phase 4** | **Auth & Profile** | Primarily Member 1 | **READY / IN PROGRESS** | May proceed in parallel with Phase 5 |
| **Phase 5** | **Skills, Availability & Matching** | Primarily Member 2 | **READY / IN PROGRESS** | May proceed in parallel with Phase 4 |
| **Phase 6** | **Swap Requests & Chat** | Primarily Member 3 | **PLANNED** | Integrates SwapRequest negotiations and SignalR chat |
| **Phase 7** | **Platform Features** | Primarily Member 5 | **PLANNED** | Reviews, notifications, badges, safety, plans |
| **Phase 8** | **Integration, Security & Hardening** | Cross-Team (All Members) | **PLANNED** | End-to-end integration, security audits, stress testing |
| **Phase 9** | **Deployment** | DevOps / Team Lead | **PLANNED** | Production CI/CD, cloud deployment, release tagging |

### Parallel Execution Guidelines:
1. **Phases 4 & 5 Parallelism:**
   - **Phase 4 (Member 1)** and **Phase 5 (Member 2)** can proceed in parallel immediately on their respective feature branches (`feature/auth-profile` and `feature/skills-matching`).
   - Both members branch from the latest `develop` branch (which contains completed Phases 1–3).
2. **Phase 6 Dependencies:**
   - **Phase 6 (Member 3)** builds on the SwapRequest/Session contracts established in Phases 2 and 3, and consumes skills from Phase 5.
3. **Phase 7 Dependencies:**
   - **Phase 7 (Member 5)** integrates post-session reviews (dependent on Phase 3 completed sessions) and safety blocks.
4. **Phases 8 & 9 Whole-Team Execution:**
   - All 5 members participate in end-to-end hardening, load testing, and deployment.

---

## 12. Emergency & Hotfix Policy

If a critical regression or financial bug is identified on `main`:
1. Branch directly from `main` into `hotfix/<issue-name>`.
2. Implement the minimal targeted fix and write an automated test reproducing and fixing the defect.
3. Member 4 and the Module Owner must review and approve the hotfix PR within 2 hours.
4. Merge the hotfix into `main` and immediately cherry-pick/rebase into `develop`.

---

## 13. The 12 Conflict-Free Operational Rules

To guarantee parallel productivity without friction, every developer must follow these 12 rules:

1. **Pull Latest Develop:** Always pull the latest `develop` branch before starting work each morning.
2. **Verify Ownership:** Check your assigned member document (`MEMBER-X-*.md`) before touching any file.
3. **Document First:** If a feature requires changes to an interface or shared DTO, document the contract proposal before implementing.
4. **Zero Silent Changes:** Never change DTO shapes, enum values, route paths, entity relationships, or business rules without explicit team agreement.
5. **Small, Focused Commits:** Commit frequently with atomic, descriptive Conventional Commits.
6. **One Logical Feature per PR:** Avoid giant PRs bundling multiple distinct features. Keep diffs small and reviewable.
7. **No Drive-By Formatting:** Do not reformat entire files or reorder usings in files outside your direct ownership.
8. **No Independent Migrations:** Never run `dotnet ef migrations add` yourself. Coordinate all schema changes through Member 4.
9. **No Implementation Duplication:** Never copy-paste or reimplement another member's service. Inject their registered interface.
10. **Strict Merge Discipline:** Code flows strictly: `feature` -> `develop` -> `main`. Direct pushes to `develop` or `main` are forbidden.
11. **Rebase Promptly:** When another feature merges into `develop`, rebase your feature branch immediately to catch conflicts early.
12. **Resolve by Authoritative Contract:** If a question arises during integration, do not guess. Consult `DATABASE.md`, `Business-Rules.md`, `FINANCIAL-ENGINE.md`, or the module owner.

---
*SkillSwap Backend Engineering Team -- Workflow Standards*
