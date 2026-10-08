# SkillSwap Backend -- Team Engineering Documentation

Welcome to the backend engineering documentation suite for the SkillSwap platform. This directory contains the authoritative architecture, workflow, ownership matrices, and boundary contracts for the 5-member backend engineering team working in parallel on [SkillSwap.sln](file:///D:/SkillSwap/SkillSwap.sln).

---

## 1. Purpose

The purpose of this documentation package is to enable 5 backend engineers to work concurrently without merge conflicts, architectural drift, domain leaks, or financial engine regressions. 

Every developer on the team has an assigned module, a defined set of files they own, a list of files they must never touch without coordination, and strict cross-module contracts.

---

## 2. Architecture Overview

SkillSwap is a peer-to-peer skill exchange and time-banking platform built on Clean Architecture principles using .NET 10 and C# 13:

- **Currency Model:** Time is platform currency. 1 conceptual credit = 30 minutes. Database stores integer minutes only.
- **Layers & Dependency Invariants:**
  - `SkillSwap.Domain`: Pure business rules, 22 domain entities, value objects, domain enums, domain exceptions. Zero external framework dependencies.
  - `SkillSwap.Application`: Application use cases, DTOs, orchestration services, and application contracts/abstractions (`IApplicationDbContext`, `IWalletService`, etc.) implemented by Infrastructure. **MUST NOT depend on `SkillSwap.Infrastructure` or `SkillSwap.API`**.
  - `SkillSwap.Infrastructure`: EF Core `ApplicationDbContext`, SQL Server persistence, ASP.NET Core Identity, hosted background services, row-level locking, and token generation.
  - `SkillSwap.API`: ASP.NET Core Web API presentation layer, REST controllers, SignalR `ChatHub`, exception middleware, and Swagger documentation. Delegates all business behavior to Application services.
  - `SkillSwap.Tests`: xUnit test suite, unit tests, and SQL Server integration tests.
- **Official Solution File:** `SkillSwap.sln` (All developers must open and build `SkillSwap.sln` exclusively).

---

## 3. Team Member Ownership Summary (22 Entities)

> **Important:** **Member Ownership** defines code and module accountability, while **Project Phases (1 to 9)** define the sequential execution roadmap. A member's module ownership spans multiple phases.

| Member | Focus Area | Primary Domain Entities (22 Total) | Lead Responsibilities |
|---|---|---|---|
| **Member 1** | **Auth & Profile** | `ApplicationUser`, `UserProfile` (2) | Identity registration, JWT issuance, roles, user profiles, current user context. Primary in Phase 4. |
| **Member 2** | **Skills, Availability & Matching** | `Category`, `Skill`, `UserSkill`, `AvailabilitySlot` (4) | Skill taxonomy, teach/learn user skills, weekly availability, computed matching engine. Primary in Phase 5. |
| **Member 3** | **Swap Request & Chat** | `SwapRequest`, `Conversation`, `ConversationParticipant`, `Message` (4) | Swap negotiations, participant messaging, SignalR chat integration (`ChatHub`). Primary in Phase 6. |
| **Member 4** | **Session & Wallet (Lead)** | `Session`, `Wallet`, `CreditTransaction` (3) | Booking escrow, wallet balance, credit ledger, completion, cancellations, no-shows, disputes, background jobs, **sole EF migration owner**. Lead in Phase 3. |
| **Member 5** | **Platform Features** | `Review`, `Notification`, `Badge`, `UserBadge`, `Favorite`, `UserBlock`, `Report`, `SubscriptionPlan`, `UserSubscription` (9) | Reviews, notifications, gamification, favorites, user blocks, reporting moderation, subscription capability flags. Primary in Phase 7. |

---

## 4. Official Project Execution Roadmap (Phases 1-9)

The project proceeds across 9 sequential delivery milestones:

- **Phase 1 -- Foundation** ✅ (COMPLETE)
- **Phase 2 -- Database & Domain** ✅ (COMPLETE: 22 entities, 12 enums, EF configurations)
- **Phase 3 -- Core Business Engine** ✅ (COMPLETE: Wallets, credit ledger, booking engine, locking, 74 passing tests)
- **Phase 4 -- Auth & Profile** (Primarily Member 1: Ready for development)
- **Phase 5 -- Skills, Availability & Matching** (Primarily Member 2: Ready for development -- **proceeds in parallel with Phase 4**)
- **Phase 6 -- Swap Requests & Chat** (Primarily Member 3: Pre-session negotiations and SignalR chat)
- **Phase 7 -- Platform Features** (Primarily Member 5: Post-session reviews, notifications, safety, and subscriptions)
- **Phase 8 -- Integration, Security & Hardening** (Cross-team integration, security audits, stress testing)
- **Phase 9 -- Deployment** (DevOps, CI/CD, production deployment)

---

## 5. Documentation Index

### Core Architecture & Workflow
- [TEAM-ARCHITECTURE.md](file:///D:/SkillSwap/docs/team/TEAM-ARCHITECTURE.md) -- Comprehensive system architecture, Clean Architecture layers, 22 entities, cross-module business contracts, complete API ownership matrix, shared file policies, and concurrency boundaries.
- [TEAM-WORKFLOW.md](file:///D:/SkillSwap/docs/team/TEAM-WORKFLOW.md) -- Git branching (`main` / `develop` / feature branches), commit format, pull request review policies, single-owner migration workflow, testing discipline, and the 12 non-conflict operational rules.

### Individual Member Ownership Guides
- [MEMBER-1-AUTH-PROFILE.md](file:///D:/SkillSwap/docs/team/MEMBER-1-AUTH-PROFILE.md) -- Authentication, ASP.NET Core Identity, JWT token generation, and UserProfile API guide.
- [MEMBER-2-SKILLS-MATCHING.md](file:///D:/SkillSwap/docs/team/MEMBER-2-SKILLS-MATCHING.md) -- Category browsing, Skill catalog, UserSkills management, AvailabilitySlots, and computed matching algorithm.
- [MEMBER-3-SWAP-CHAT.md](file:///D:/SkillSwap/docs/team/MEMBER-3-SWAP-CHAT.md) -- SwapRequest lifecycle, Requester/Receiver role contract, Conversation management, and SignalR real-time messaging.
- [MEMBER-4-SESSION-WALLET.md](file:///D:/SkillSwap/docs/team/MEMBER-4-SESSION-WALLET.md) -- Core financial engine, Session lifecycle, Wallet escrow, CreditTransaction ledger, deterministic locking, and migration authority.
- [MEMBER-5-PLATFORM.md](file:///D:/SkillSwap/docs/team/MEMBER-5-PLATFORM.md) -- Session reviews, notifications, user badges, favorites, user blocks, moderation reports, and subscription capability tiers.

---

## 6. Source of Truth Hierarchy

When resolving technical or business questions, use the following strict hierarchy:

```
1. Actual Executable Code (src/, tests/)
       v
2. docs/DATABASE.md
       v
3. docs/Business-Rules.md
       v
4. docs/FINANCIAL-ENGINE.md
       v
5. docs/team/ (This Team Documentation Suite)
```

> **CRITICAL RULE:**
> If a team document conflicts with implemented executable code or an authoritative business document, the conflict **MUST NOT be silently ignored**. It must be raised immediately in team standup or PR discussion and reconciled against the authoritative source.

---

## 7. How to Use These Docs

1. **New Backend Onboarding:**
   - Read this `README.md`, followed by `TEAM-ARCHITECTURE.md` and `TEAM-WORKFLOW.md`.
   - Read your assigned member document (`MEMBER-X-*.md`) from start to finish.
   - Clone the repository, open `SkillSwap.sln`, and execute `dotnet test` to verify that all existing tests pass before writing code.
2. **Before Starting Any Feature:**
   - Verify what endpoints and services are marked `IMPLEMENTED` vs `PLANNED`. Never recreate an implemented service.
   - Check the **"Files I Own"** and **"Files I Must Not Modify Without Coordination"** sections in your member guide.
3. **When Crossing Module Boundaries:**
   - Consult the **Cross-Module Contracts** section in `TEAM-ARCHITECTURE.md`.
   - Never directly modify another member's entities or services. Consume existing abstractions (`IWalletService`, `ISessionService`, etc.) or request an interface update via PR review.
4. **When Modifying Database Schemas:**
   - Stop. Only **Member 4** runs `dotnet ef migrations add`. Follow the schema update protocol defined in `TEAM-WORKFLOW.md`.

---

## 8. Terminology Quick Reference

| Term | Strict Definition | Common Mistake to Avoid |
|---|---|---|
| **Requester** | The user who initiates a `SwapRequest` to learn a skill (`Learner`). | Never treat Requester as Teacher. |
| **Receiver** | The user who receives the `SwapRequest` and teaches the skill (`Teacher`). | Never treat Receiver as Learner. |
| **Learner** | The session participant who receives knowledge and spends escrowed minutes. | Never debit minutes from Teacher. |
| **Teacher** | The session participant who imparts knowledge and earns minutes upon completion. | Never hold minutes from Teacher. |
| **SwapRequest** | The pre-session negotiation artifact. Never has a `Completed` status. | Never mark SwapRequest as Completed. |
| **Session** | The scheduled event where learning takes place. Tracks actual held minutes. | Do not confuse Session with SwapRequest. |
| **Wallet** | Materialized balance (`AvailableMinutes`, `HeldMinutes`). Never negative. | Never modify Wallet without a matching ledger entry. |
| **CreditTransaction** | Immutable, append-only financial audit trail record. | Never delete or mutate CreditTransactions. |
| **Monthly Quota** | Calendar-month learning cap (Free: 180m, Premium: 720m) derived from sessions. | Quota is NOT stored in Wallet. |
| **Auto-Completion** | System background job completing sessions past `EndUtc + GracePeriod`. | Does not require an authenticated user identity. |
| **Dispute Resolution** | Administrative intervention (`POST /api/admin/sessions/{id}/resolve-dispute`). | Participants can never arbitrate disputes. |

---
*SkillSwap Backend Engineering Team -- Official Documentation*
