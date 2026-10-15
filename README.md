# SkillSwap Backend API

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp)](https://learn.microsoft.com/dotnet/csharp/)
[![EF Core 10](https://img.shields.io/badge/EF%20Core-10.0-512BD4)](https://learn.microsoft.com/ef/core/)
[![Tests](https://img.shields.io/badge/Tests-351%20Passing-brightgreen)](tests/SkillSwap.Tests/)
[![Deployment Status](https://img.shields.io/badge/Production-Live-success)](https://skill-swap.runasp.net)

> A production-ready peer-to-peer skill exchange and time-banking platform backend built with Clean Architecture, ASP.NET Core Web API, Entity Framework Core 10, Microsoft SQL Server, and SignalR.

---

## 🌐 Live Production Deployment

SkillSwap's backend is deployed and running in production on **MonsterASP.NET**:

| Resource | Production Link | Description |
|---|---|---|
| **API Base URL** | [https://skill-swap.runasp.net](https://skill-swap.runasp.net) | Production API root |
| **Interactive Swagger UI** | [https://skill-swap.runasp.net/swagger/index.html](https://skill-swap.runasp.net/swagger/index.html) | OpenAPI documentation & interactive client |
| **Health Check** | [https://skill-swap.runasp.net/health](https://skill-swap.runasp.net/health) | Live infrastructure & database health probe |
| **SignalR Chat Hub** | `wss://skill-swap.runasp.net/hubs/chat` | Real-time WebSocket messaging endpoint |

> **Operational Documentation:** Detailed configuration, environment setup, database migrations, and operational recovery runbooks are documented in [`docs/deployment/PRODUCTION_DEPLOYMENT.md`](docs/deployment/PRODUCTION_DEPLOYMENT.md).

---

## 💡 What is SkillSwap?

SkillSwap is a decentralized, peer-to-peer knowledge exchange and time-banking platform. Rather than using conventional currency, SkillSwap enables users to teach skills they possess and learn skills from others using **time credits**:

- **Currency Unit:** Time is the fundamental platform currency: **1 credit = 30 minutes**.
- **Storage Rule:** All account balances and ledger entries are strictly stored as integer minutes.
- **Fair Exchange:** When a user learns, time minutes are held in escrow and transferred to the teacher upon verified session completion. When a user teaches, they earn time minutes to spend learning from peers.
- **Pre-Session Negotiation:** Pre-session proposals and scheduling details are coordinated through structured swap requests and real-time messaging before any time credits are committed.

---

## 🏗️ Technology Stack

| Component | Technology / Library | Details |
|---|---|---|
| **Runtime Framework** | .NET 10 (`net10.0`) | ASP.NET Core Web API, InProcess IIS hosting |
| **Language** | C# 13 | Nullable reference types, file-scoped namespaces, pattern matching |
| **Architecture** | Clean Architecture | Strict separation of Domain, Application, Infrastructure, and API |
| **ORM / Data Access** | Entity Framework Core 10 | Code-first migrations, compiled models, fluent configurations |
| **Database Engine** | Microsoft SQL Server | Relational schema, row-level `UPDLOCK, ROWLOCK` locking, filtered unique indexes |
| **Identity & Security** | ASP.NET Core Identity | `IdentityUser<Guid>`, PBKDF2 password hashing, account lockouts |
| **Authentication** | JWT Bearer Authentication | HMAC-SHA256 signed access tokens, startup entropy validation (`JwtSettingsValidator`) |
| **Real-Time Communication** | ASP.NET Core SignalR | `ChatHub` over WebSockets with group authorization |
| **API Documentation** | OpenAPI / Swashbuckle Swagger | Schema generation, bearer auth support, XML documentation |
| **Test Suites** | xUnit, FluentAssertions, Moq | 351 automated tests (unit tests and SQL Server integration tests) |

---

## 🏛️ Clean Architecture Overview

SkillSwap strictly enforces Clean Architecture dependency rules: dependencies point inward toward the Domain model, and the core business logic remains independent of databases, presentation frameworks, and third-party libraries.

```
SkillSwap/
├── src/
│   ├── SkillSwap.Domain/          # Pure business rules, 22 entities, enums, domain exceptions
│   │   ├── Entities/              # ApplicationUser, Session, Wallet, CreditTransaction, etc.
│   │   ├── Enums/                 # SessionStatus, SwapRequestStatus, CreditTransactionType, etc.
│   │   └── Exceptions/            # InsufficientBalanceException, QuotaExceededException, etc.
│   ├── SkillSwap.Application/     # Application use cases, orchestration, DTOs, abstractions
│   │   ├── Abstractions/          # Service contracts: IWalletService, ISessionService, etc.
│   │   ├── DTOs/                  # Strongly typed request/response contracts
│   │   └── Services/              # Core business services: WalletService, SessionService, etc.
│   ├── SkillSwap.Infrastructure/  # EF Core, SQL Server persistence, Identity, background jobs
│   │   ├── Persistence/           # ApplicationDbContext, fluent entity configurations
│   │   ├── Migrations/            # EF Core schema migration history and model snapshot
│   │   ├── Identity/              # IdentityRoleSeeder, custom user stores
│   │   └── Services/              # SqlWalletLockService, JwtTokenService, BackgroundServices
│   └── SkillSwap.API/             # Presentation layer, controllers, SignalR hub, middleware
│       ├── Controllers/           # REST endpoints (Auth, Sessions, SwapRequests, etc.)
│       ├── Hubs/                  # ChatHub (SignalR WebSocket messaging)
│       └── Middleware/            # GlobalExceptionHandlingMiddleware
└── tests/
    └── SkillSwap.Tests/           # Unit & SQL Server concurrency integration tests
```

---

## ⚖️ Core Domain & Financial Invariants

1. **Non-Negative Wallets:** Wallet balances (`AvailableMinutes`, `HeldMinutes`) can never drop below zero under any circumstance.
2. **Escrow Lifecycle:**
   - **Booking:** When a learner schedules a session, the required minutes are deducted from `AvailableMinutes` and added to `HeldMinutes`.
   - **Completion:** Upon successful completion, minutes are deducted from the learner's `HeldMinutes` and credited to the teacher's `AvailableMinutes`.
   - **Cancellation:** If a session is cancelled or expires, held minutes are immediately refunded to the learner's `AvailableMinutes`.
3. **Deterministic Row-Level Locking:** To prevent deadlocks and concurrent balance manipulation, all wallet operations acquire row-level locks ordered deterministically by user GUID: `MIN(LearnerId, TeacherId)` followed by `MAX(LearnerId, TeacherId)` using `UPDLOCK, ROWLOCK`.
4. **Financial Consistency & Idempotency:** Every wallet mutation is recorded in the append-only `CreditTransactions` ledger within the same database transaction. Administrative bonus grants enforce idempotency keys via the database filtered unique index `UX_CreditTransaction_IdempotencyKey`.
5. **Double-Completion Prevention:** Completed sessions are protected against race conditions via atomic conditional updates (`UPDATE Sessions SET Status = Completed WHERE Id = @Id AND Status = Scheduled`).
6. **Monthly Learning Quotas:**
   - **Free Plan:** 180 minutes of learning per calendar month.
   - **Premium Plan:** 720 minutes of learning per calendar month.
   - Teaching is completely uncapped across all plans.
7. **Pre-Session Negotiation Contract:**
   - `SwapRequest.RequesterId` is always the Learner; `SwapRequest.ReceiverId` is always the Teacher. Roles are immutable.
   - A `SwapRequest` remains in status `Accepted` and never transitions to `Completed`; it serves as a persistent authorization parent for scheduling sessions.

---

## 💬 Real-Time Chat & SignalR Messaging

SkillSwap combines real-time WebSocket communication with REST endpoints:

- **SignalR Hub:** Mapped to `/hubs/chat` (`ChatHub`).
- **Connection Authentication:** Clients pass their JWT token in the query string (`?access_token=<jwt>`), which is intercepted by `AuthenticationExtensions.cs`.
- **Group Routing & Authorization:** Clients join real-time groups keyed by conversation (`conversation_{id}`). `ChatHub.JoinConversation` verifies caller membership against `ConversationParticipants` before granting access.
- **REST Fallback:** Clients can send messages via `POST /api/conversations/{id}/messages` and retrieve paginated history via `GET /api/conversations/{id}/messages`. Messages sent via REST are saved to the database first and then broadcast to the SignalR group.

---

## 🗄️ Database Migrations & Seeding

The database schema is managed via Entity Framework Core migrations:

1. `20261008143422_InitialCreate`: Core schema, 22 relational tables, foreign key constraints, and default subscription plan seed.
2. `20261008154444_HardenPhase3Contracts`: Nullable plan pricing contract hardening.
3. `20261010190051_AddCreditTransactionIdempotencyKey`: Credit ledger idempotency column and unique filtered index.

### Automated Seed Data
- **Subscription Plans:** `FREE` (180 min, Price = $0.00) and `PREMIUM` (720 min, Price = NULL) are seeded via EF Core migration.
- **Identity Roles:** `Admin` and `User` roles are verified and seeded upon application startup by `IdentityRoleSeederHostedService`.
- **Badge Catalog:** The `Badges` and `UserBadges` entities, database tables, and retrieval endpoints (`GET /api/badges`, `GET /api/users/{id}/badges`) are fully implemented. Fresh databases return `[]` until achievement definitions are populated through administrative seed scripts.

---

## ⚙️ Configuration & Environment Variables

Production deployments require environment variables injected via the hosting provider or operating system. **Never commit secrets to source control.**

### Required Environment Variables

| Variable Name | Description |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Declares ASP.NET Core environment (`Production` or `Development`). |
| `ConnectionStrings__DefaultConnection` | Microsoft SQL Server connection string (use provider Local Connection String for hosted deployments). |
| `Jwt__SecretKey` | Symmetric cryptographic key for HMAC-SHA256 signing (minimum 32 bytes / 256 bits). Validated at startup. |
| `Jwt__Issuer` | Trusted JWT token issuer claim (`SkillSwap`). |
| `Jwt__Audience` | Intended token audience claim (`SkillSwap.Client`). |
| `Swagger__Enabled` | Set to `true` to enable Swagger UI in Production for API evaluation. |
| `AdminUser__Email` | Email address for initial platform administrator provisioning. |
| `AdminUser__Password` | High-entropy password for initial platform administrator. |
| `AdminUser__DisplayName` | Profile display name for initial administrator account. |

---

## 🚀 Local Setup & Development

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (v10.0 or higher)
- Microsoft SQL Server (Local instance, SQL Server Express, LocalDB, or Docker container)

### 1. Clone & Build
```bash
git clone https://github.com/islamkhaled1/SkillSwap.git
cd SkillSwap
dotnet restore SkillSwap.sln
dotnet build SkillSwap.sln --configuration Release
```

### 2. Run Test Suite
Execute the full automated test suite (351 passing tests):
```bash
dotnet test SkillSwap.sln --configuration Release
```

### 3. Configure Local Secrets
Configure a local disposable database connection and development JWT settings via .NET User Secrets:
```bash
cd src/SkillSwap.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=SkillSwapDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
dotnet user-secrets set "Jwt:SecretKey" "DEVELOPMENT_ONLY_INSECURE_TEST_SIGNING_KEY_FOR_LOCAL_DEV_ENVIRONMENT_DO_NOT_USE_IN_PRODUCTION"
```

### 4. Apply Migrations
```bash
dotnet ef database update --project src/SkillSwap.Infrastructure --startup-project src/SkillSwap.API
```

### 5. Run the Application
```bash
dotnet run --project src/SkillSwap.API
```

Once started, the API is accessible at:
- **Default Local URL:** `http://localhost:5284`
- **Local Swagger UI:** `http://localhost:5284/swagger`
- **Health Check Probe:** `http://localhost:5284/health`
- **SignalR Chat Hub:** `ws://localhost:5284/hubs/chat`

---

## 📮 API Testing with Postman

A comprehensive Postman test collection covering all 15 functional modules is provided in the repository at:
[`docs/postman/SkillSwap.postman_collection.json`](docs/postman/SkillSwap.postman_collection.json)

The collection includes end-to-end tests for:
1. Health Probes (`/health`, `/api/health`)
2. Identity & Registration (`/api/auth/register`, `/api/auth/login`)
3. User Profiles (`/api/profiles/me`, `/api/profiles/{id}`)
4. Skills Catalog & User Skills (`/api/categories`, `/api/skills`, `/api/user-skills`)
5. Weekly Availability Schedules (`/api/availability/me`)
6. Dynamic Matching Engine (`/api/matches`)
7. Swap Requests Negotiation (`/api/swap-requests`, accept, decline, cancel)
8. 1-on-1 Conversations & REST Messaging (`/api/conversations`)
9. Session Booking & Escrow (`/api/sessions`, confirm, join, complete, cancel, no-show, dispute)
10. Wallet Balances & Credit Ledger (`/api/sessions`, `/api/admin/wallets`)
11. Subscription Tiers & Mock Upgrades (`/api/subscriptions`)
12. Session Reviews & Public Ratings (`/api/reviews`)
13. In-App Notifications (`/api/notifications`)
14. Social & Safety: Badges, Favorites, Blocks, Moderation Reports (`/api/badges`, `/api/favorites`, `/api/blocks`, `/api/reports`)
15. Administrator Operations & Idempotent Bonus Grants (`/api/admin/wallets/{userId}/bonus` with `Idempotency-Key`)

---

## 👥 Engineering Team & Contributions

All active engineering, architecture, and deployment responsibilities are maintained by exactly four developers:

### [Developer 1 — Islam Khaled](https://github.com/islamkhaled1)
- **Module:** Financial Engine, Sessions, Swap Lifecycle, Real-Time Chat & Database Migrations Lead
- **Developer 1 — Production Deployment & Hosting:** Production deployment, hosting configuration, and operational maintenance of the live SkillSwap API on MonsterASP.NET ([https://skill-swap.runasp.net](https://skill-swap.runasp.net)).
- **Contributions:** Core time-banking financial engine, wallet balances and escrow lifecycle, append-only credit ledger (`CreditTransaction`), deterministic row-level concurrency locking (`SqlWalletLockService`), idempotency enforcement (`UX_CreditTransaction_IdempotencyKey`), administrative bonus grants, double-completion protection, session scheduling and arbitration, SwapRequest pre-session negotiation lifecycle, SignalR `ChatHub` real-time communication, background maintenance workers, and exclusive ownership of all EF Core database migrations.
- **Documentation:** [`docs/team/DEVELOPER-1-SESSIONS-WALLET-SWAP-CHAT.md`](docs/team/DEVELOPER-1-SESSIONS-WALLET-SWAP-CHAT.md) & [`docs/deployment/PRODUCTION_DEPLOYMENT.md`](docs/deployment/PRODUCTION_DEPLOYMENT.md)

### [Developer 2 — Arwa Khalid](https://github.com/ArwaKhalidd)
- **Module:** Platform Features & Conversations REST API
- **Contributions:** Post-session ratings and reviews (`Review`), in-app user notifications center (`Notification`), gamification achievement catalog and user badges (`Badge`, `UserBadge`), social favorites bookmarking, user blocking safety controls (`UserBlock`), moderation reporting (`Report`), subscription tier definitions and learning caps (`SubscriptionPlan`), and the **ConversationsController REST API** (conversation listing, metadata retrieval, paginated message history, REST message dispatch, read cursor updates, DTO validation, and REST controller tests).
- **Documentation:** [`docs/team/DEVELOPER-2-PLATFORM-CONVERSATIONS.md`](docs/team/DEVELOPER-2-PLATFORM-CONVERSATIONS.md)

### [Developer 3 — Mario Wael](https://github.com/mariowael)
- **Module:** Skills Catalog, Availability & Dynamic Matching
- **Contributions:** Category hierarchy and master skills catalog, user skill capability profiling (teach/learn declarations and proficiency levels), recurring weekly availability schedules (`AvailabilitySlot`), and the dynamic computed matching algorithm evaluating teach/learn complementarity and availability overlaps.
- **Documentation:** [`docs/team/DEVELOPER-3-SKILLS-MATCHING.md`](docs/team/DEVELOPER-3-SKILLS-MATCHING.md)

### [Developer 4 — Kholoud Khaled](https://github.com/kholoudkhaled191)
- **Module:** Authentication, Identity, Security & Profiles
- **Contributions:** ASP.NET Core Identity integration, user registration and login workflows, password hashing and lockout policies, JWT access token generation, startup security validation (`JwtSettingsValidator`), current user context resolution (`ICurrentUserService`), profile management (`UserProfile`), and user onboarding bootstrapping.
- **Documentation:** [`docs/team/DEVELOPER-4-AUTH-PROFILE.md`](docs/team/DEVELOPER-4-AUTH-PROFILE.md)

---

## 📄 Team Engineering Guides

The complete internal engineering documentation suite is located in [`docs/team/`](docs/team/):

- [`docs/team/README.md`](docs/team/README.md) — Team documentation index, source-of-truth hierarchy, and terminology disambiguation.
- [`docs/team/TEAM-ARCHITECTURE.md`](docs/team/TEAM-ARCHITECTURE.md) — Comprehensive technical architecture, Clean Architecture layers, 22 entities, cross-module contracts, and API ownership matrix.
- [`docs/team/TEAM-WORKFLOW.md`](docs/team/TEAM-WORKFLOW.md) — Git branching standards, PR review policies, sole-owner migration protocol, and quality gates.
- [`docs/deployment/PRODUCTION_DEPLOYMENT.md`](docs/deployment/PRODUCTION_DEPLOYMENT.md) — Production deployment runbook for MonsterASP.NET.

---

## 📜 License

This project is developed for educational and portfolio demonstration purposes. All rights reserved.
