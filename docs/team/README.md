# SkillSwap Backend — Team Engineering Documentation

Welcome to the backend engineering documentation suite for the SkillSwap platform. This directory contains the authoritative architecture, workflow, ownership matrices, and boundary contracts for the 4-developer backend engineering team working on [SkillSwap.sln](file:///D:/SkillSwap/SkillSwap.sln).

---

## 1. Purpose

The purpose of this documentation package is to enable 4 backend developers to collaborate effectively without merge conflicts, architectural drift, domain leaks, or financial engine regressions.

Every developer on the team has an assigned module, a defined set of files they own, a list of cross-module dependencies, and strict boundary contracts.

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

## 3. Team Developer Ownership Summary (22 Entities)

All active engineering and operational responsibilities are structured across exactly **four developers**:

| Developer | Focus Area & Module | Primary Domain Entities (22 Total) | Lead Responsibilities & Ownership |
|---|---|---|---|
| **Developer 1**<br>[Islam Khaled](https://github.com/islamkhaled1) | **Sessions, Wallet, Swap Lifecycle, Real-Time Chat & Hosting** | `Session`, `Wallet`, `CreditTransaction`, `SwapRequest`, `Conversation`, `ConversationParticipant`, `Message` (7) | Core financial engine, booking escrow, wallet balances, append-only credit ledger, idempotency keys, deterministic row-level locks, SwapRequest lifecycle, SignalR `ChatHub` real-time communication, background maintenance workers, **exclusive EF Core migrations lead**, and **Developer 1 — Production Deployment & Hosting**. |
| **Developer 2**<br>[Arwa Khalid](https://github.com/ArwaKhalidd) | **Platform Features & Conversations REST API** | `Review`, `Notification`, `Badge`, `UserBadge`, `Favorite`, `UserBlock`, `Report`, `SubscriptionPlan`, `UserSubscription` (9) | Post-session reviews, notifications center, gamification badges, favorites, user blocks, moderation reports, subscription tier capabilities, and **ConversationsController REST API** (conversation listing, message history, REST dispatch, and read receipts). |
| **Developer 3**<br>[Mario Wael](https://github.com/mariowael) | **Skills, Availability & Matching** | `Category`, `Skill`, `UserSkill`, `AvailabilitySlot` (4) | Skill taxonomy, teach/learn user skill profiles, weekly recurring availability schedules, dynamic computed matching engine (no persisted match entity). |
| **Developer 4**<br>[Kholoud Khaled](https://github.com/kholoudkhaled191) | **Auth & Profile** | `ApplicationUser`, `UserProfile` (2) | Identity registration, login, JWT token issuance, security policies, user profiles, current user context, startup token validation. |

---

## 4. Documentation Index

### Core Architecture & Workflow
- [TEAM-ARCHITECTURE.md](TEAM-ARCHITECTURE.md) — Comprehensive system architecture, Clean Architecture layers, 22 entities, cross-module business contracts, complete API ownership matrix, shared file policies, and concurrency boundaries.
- [TEAM-WORKFLOW.md](TEAM-WORKFLOW.md) — Git branching (`main` / `develop` / feature branches), commit format, pull request review policies, single-owner migration workflow, testing discipline, and operational rules.
- [PRODUCTION_DEPLOYMENT.md](../deployment/PRODUCTION_DEPLOYMENT.md) — Production deployment guide for MonsterASP.NET hosting, environment variables, local connection string, and migration verification.

### Individual Developer Ownership Guides
- [DEVELOPER-1-SESSIONS-WALLET-SWAP-CHAT.md](DEVELOPER-1-SESSIONS-WALLET-SWAP-CHAT.md) — Core financial engine, Session lifecycle, Wallet escrow, CreditTransaction ledger, row-level locking, SwapRequest lifecycle, SignalR `ChatHub`, migration authority, and Developer 1 — Production Deployment & Hosting ([Islam Khaled](https://github.com/islamkhaled1)).
- [DEVELOPER-2-PLATFORM-CONVERSATIONS.md](DEVELOPER-2-PLATFORM-CONVERSATIONS.md) — Platform features (Reviews, Notifications, Badges, Favorites, Blocks, Reports, Subscriptions) and Conversations REST API ([Arwa Khalid](https://github.com/ArwaKhalidd)).
- [DEVELOPER-3-SKILLS-MATCHING.md](DEVELOPER-3-SKILLS-MATCHING.md) — Category browsing, Skill catalog, UserSkills management, AvailabilitySlots, and computed matching algorithm ([Mario Wael](https://github.com/mariowael)).
- [DEVELOPER-4-AUTH-PROFILE.md](DEVELOPER-4-AUTH-PROFILE.md) — Authentication, ASP.NET Core Identity, JWT token generation, security validation, and UserProfile API guide ([Kholoud Khaled](https://github.com/kholoudkhaled191)).

---

## 5. Source of Truth Hierarchy

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
> If a team document conflicts with implemented executable code or an authoritative business document, the conflict **MUST NOT be silently ignored**. It must be raised immediately and reconciled against the authoritative source.

---

## 6. How to Use These Docs

1. **Developer Onboarding:**
   - Read this `README.md`, followed by `TEAM-ARCHITECTURE.md` and `TEAM-WORKFLOW.md`.
   - Read your assigned developer document (`DEVELOPER-X-*.md`) from start to finish.
   - Build `SkillSwap.sln` and execute `dotnet test` to verify that all existing tests pass before making modifications.
2. **Feature Development:**
   - Check the **"Files Owned"** and **"Cross-Module Contracts"** sections in your developer guide.
3. **Crossing Module Boundaries:**
   - Consult the **Cross-Module Contracts** section in `TEAM-ARCHITECTURE.md`.
   - Never directly modify another developer's core entities or services without coordination. Consume existing abstractions (`IWalletService`, `ISessionService`, `IChatService`, etc.).
4. **Modifying Database Schemas:**
   - Stop. Only **Developer 1** executes `dotnet ef migrations add`. Submit schema updates as EF Core configuration classes.

---

## 7. Terminology Quick Reference

| Term | Strict Definition | Common Mistake to Avoid |
|---|---|---|
| **Requester** | The user who initiates a `SwapRequest` to learn a skill (`Learner`). | Never treat Requester as Teacher. |
| **Receiver** | The user who receives the `SwapRequest` and teaches the skill (`Teacher`). | Never treat Receiver as Learner. |
| **Learner** | The session participant who receives knowledge and spends escrowed minutes. | Never debit minutes from Teacher. |
| **Teacher** | The session participant who imparts knowledge and earns minutes upon completion. | Never hold minutes from Teacher. |
| **SwapRequest** | The pre-session negotiation artifact. Never has a `Completed` status. | Never mark SwapRequest as Completed. |
| **Session** | The scheduled event where learning takes place. Tracks actual held minutes. | Do not confuse Session with SwapRequest. |
| **Wallet** | Materialized balance (`AvailableMinutes`, `HeldMinutes`). Never negative. | Never modify Wallet without a matching ledger entry. |
| **CreditTransaction** | Immutable, append-only financial audit trail record with idempotency key. | Never delete or mutate CreditTransactions. |
| **Monthly Quota** | Calendar-month learning cap (Free: 180m, Premium: 720m) derived from sessions. | Quota is NOT stored in Wallet. |
| **Auto-Completion** | System background job completing sessions past `EndUtc + GracePeriod`. | Does not require an authenticated user identity. |
| **Dispute Resolution** | Administrative intervention (`POST /api/sessions/{id}/dispute`). | Participants can never arbitrate disputes. |

---
*SkillSwap Backend Engineering Team — Official Documentation*
