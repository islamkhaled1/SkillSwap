# Developer 1 — Sessions, Wallet, Swap Lifecycle, Real-Time Chat & Deployment

**Developer:** [Islam Khaled](https://github.com/islamkhaled1) (@islamkhaled1)  
**Role:** Developer 1  
**Module:** Financial Engine, Sessions, Swap Requests, SignalR ChatHub, Migrations & Hosting  
**Special Recognition:** Production Deployment & Hosting — Islam Khaled  
---

## 1. Mission

Serve as the platform's core financial, transactional, and operational authority. Own the time-banking ledger, wallet balances, escrow lifecycle, session booking and scheduling engine, deterministic row-level locking, double-completion protection, monthly quota enforcement, SwapRequest pre-session negotiation lifecycle, SignalR real-time messaging hub (`ChatHub`), act as the **sole authority for Entity Framework Core migrations**, and lead the **production deployment and hosting on MonsterASP.NET**.

---

## 2. Scope of Ownership

- **Financial Engine:** Wallet balance state (`AvailableMinutes`, `HeldMinutes`), append-only credit ledger (`CreditTransaction`), ledger reconciliation, and SQL Server row-level locking (`SqlWalletLockService`).
- **Financial Consistency & Idempotency:** Enforcing transactional boundaries and deduplication via `UX_CreditTransaction_IdempotencyKey` on bonus and credit adjustments.
- **Session Lifecycle & Escrow:** Booking validation, teacher confirmation, participant join tracking, session completion, cancellation, no-show reporting, and dispute escalation.
- **Swap Request Lifecycle:** Pre-session proposals (`Pending`, `Accepted`, `Declined`, `Cancelled`, `Expired`), role validation (Requester = Learner, Receiver = Teacher), and coordinating conversation provisioning upon acceptance.
- **Real-Time Communication (SignalR):** `ChatHub` mapped to `/hubs/chat`, token query-string authentication, connection lifecycle, conversation membership verification, and group broadcasting (`conversation_{id}`).
- **Background Maintenance Workers:** Automated session completion worker (`SessionAutoCompletionBackgroundService`) and pending session expiration worker (`PendingSessionExpirationBackgroundService`).
- **Administrative Operations:** Administrative bonus credit endpoint with idempotency headers (`/api/admin/wallets/{userId}/bonus`) and dispute resolution.
- **Database Migrations:** Single exclusive owner of EF Core migrations in `src/SkillSwap.Infrastructure/Migrations/*` and `ApplicationDbContextModelSnapshot.cs`.
- **Production Deployment & Hosting:** Configuration, publishing, and ongoing operational maintenance of the live SkillSwap API on MonsterASP.NET (`https://skill-swap.runasp.net`).

---

## 3. Key Architectural Invariants & Business Rules

1. **Non-Negative Wallets:** Wallet balances (`AvailableMinutes`, `HeldMinutes`) can **never** become negative under any circumstance.
2. **Strict Escrow Flow:** When a session is booked, minutes are moved from `AvailableMinutes` to `HeldMinutes` of the Learner. Upon successful completion, minutes are debited from the Learner's `HeldMinutes` and credited to the Teacher's `AvailableMinutes`.
3. **Deterministic Row-Level Locking:** All wallet mutations acquire row-level locks on both participants' wallets ordered by `UserId` (`MIN(LearnerId, TeacherId)` then `MAX(LearnerId, TeacherId)`) using `UPDLOCK, ROWLOCK` to eliminate deadlocks.
4. **Idempotency Protection:** Administrative bonus grants and financial adjustments enforce idempotency keys backed by the unique filtered index `UX_CreditTransaction_IdempotencyKey`. Replaying the same key with identical payload returns the original result safely.
5. **Requester / Receiver Role Invariant:** In a `SwapRequest`, `RequesterId` is always the Learner and `ReceiverId` is always the Teacher. Roles are never inverted.
6. **SwapRequest Status Invariant:** Swap requests negotiate readiness and persist as parent authorizations. A `SwapRequest` **never** has a `Completed` status.
7. **SignalR Group Authorization:** `ChatHub.JoinConversation` verifies caller membership in `ConversationParticipants` before admitting a connection to `conversation_{id}`.

---

## 4. Domain Entities Owned

| Entity | Primary Key | Table | Description |
|---|---|---|---|
| `Session` | `long` | `Sessions` | Scheduled learning session tracking teacher, learner, skill, start/end UTC, status, dispute, and no-show flags. |
| `Wallet` | `Guid` | `Wallets` | Materialized balance record holding `AvailableMinutes` and `HeldMinutes` for a user. |
| `CreditTransaction` | `long` | `CreditTransactions` | Immutable, append-only ledger transaction recording deltas and idempotency keys. |
| `SwapRequest` | `long` | `SwapRequests` | Proposal from learner to teacher to exchange skills. |
| `Conversation` | `long` | `Conversations` | 1-on-1 message container linked 1:1 with an accepted SwapRequest. |
| `ConversationParticipant` | `(long, Guid)` | `ConversationParticipants` | Tracks user membership in conversation and `LastReadMessageId`. |
| `Message` | `long` | `Messages` | Individual chat message record. |

### Enums Owned:
- `CreditTransactionType` (`Bonus = 1`, `Hold = 2`, `Release = 3`, `Capture = 4`, `Earn = 5`, `Adjustment = 6`).
- `NoShowParty` (`Learner = 1`, `Teacher = 2`, `Both = 3`).
- `SessionMode` (`Online = 1`, `InPerson = 2`).
- `SessionStatus` (`PendingConfirmation = 1`, `Scheduled = 2`, `Completed = 3`, `Cancelled = 4`, `NoShow = 5`, `Disputed = 6`, `Expired = 7`).
- `SwapRequestStatus` (`Pending = 1`, `Accepted = 2`, `Declined = 3`, `Cancelled = 4`, `Expired = 5`).

---

## 5. Application Services & Hubs Owned

| Interface / Class | Location | Status | Description |
|---|---|---|---|
| `IWalletService` | `src/SkillSwap.Application/Abstractions/IWalletService.cs` | **IMPLEMENTED** | Wallet queries, balance transfers, bonus grants, and adjustments. |
| `WalletService` | `src/SkillSwap.Application/Services/WalletService.cs` | **IMPLEMENTED** | Implements non-negative wallet mutation and idempotency invariants. |
| `ICreditLedgerService` | `src/SkillSwap.Application/Abstractions/ICreditLedgerService.cs` | **IMPLEMENTED** | Appends and queries immutable ledger transactions. |
| `CreditLedgerService` | `src/SkillSwap.Application/Services/CreditLedgerService.cs` | **IMPLEMENTED** | Implements append-only ledger operations. |
| `ISessionService` | `src/SkillSwap.Application/Abstractions/ISessionService.cs` | **IMPLEMENTED** | Session booking, confirmation, joining, disputes, and cancellations. |
| `SessionService` | `src/SkillSwap.Application/Services/SessionService.cs` | **IMPLEMENTED** | Session orchestration and deterministic wallet locking coordination. |
| `ISessionCompletionService` | `src/SkillSwap.Application/Abstractions/ISessionCompletionService.cs` | **IMPLEMENTED** | Atomic completion and escrow transfer engine. |
| `SessionCompletionService` | `src/SkillSwap.Application/Services/SessionCompletionService.cs` | **IMPLEMENTED** | Implements double-completion prevention and escrow settlement. |
| `IQuotaService` | `src/SkillSwap.Application/Abstractions/IQuotaService.cs` | **IMPLEMENTED** | Derives monthly learning usage from committed sessions. |
| `QuotaService` | `src/SkillSwap.Application/Services/QuotaService.cs` | **IMPLEMENTED** | Implements 180m/720m calendar-month cap calculation. |
| `ISwapRequestService` | `src/SkillSwap.Application/Abstractions/ISwapRequestService.cs` | **IMPLEMENTED** | Manages swap proposals, status transitions, and conversation bootstrapping. |
| `SwapRequestService` | `src/SkillSwap.Application/Services/SwapRequestService.cs` | **IMPLEMENTED** | Implements pre-session negotiation workflows. |
| `ChatHub` | `src/SkillSwap.API/Hubs/ChatHub.cs` | **IMPLEMENTED** | SignalR real-time messaging hub (`/hubs/chat`). |
| `SqlWalletLockService` | `src/SkillSwap.Infrastructure/Services/SqlWalletLockService.cs` | **IMPLEMENTED** | Acquires raw SQL row-level locks on wallets ordered by user ID. |

---

## 6. Controllers & Endpoints Owned

| Controller | Route Area | Method & Endpoint | Description |
|---|---|---|---|
| `SessionsController` | `/api/sessions` | `POST /api/sessions` | Book new session with escrow hold. |
| `SessionsController` | `/api/sessions` | `GET /api/sessions/{id}` | Retrieve session details. |
| `SessionsController` | `/api/sessions` | `POST /api/sessions/{id}/confirm` | Teacher confirms scheduled session. |
| `SessionsController` | `/api/sessions` | `POST /api/sessions/{id}/join` | Mark participant as joined. |
| `SessionsController` | `/api/sessions` | `POST /api/sessions/{id}/complete` | Settle completed session and release escrow to teacher. |
| `SessionsController` | `/api/sessions` | `POST /api/sessions/{id}/cancel` | Cancel session and refund escrow to learner. |
| `SessionsController` | `/api/sessions` | `POST /api/sessions/{id}/no-show` | Report participant no-show. |
| `SessionsController` | `/api/sessions` | `POST /api/sessions/{id}/dispute` | Escalate session dispute. |
| `SwapRequestsController` | `/api/swap-requests` | `POST /api/swap-requests` | Submit new swap proposal to a teacher. |
| `SwapRequestsController` | `/api/swap-requests` | `GET /api/swap-requests/{id}` | Retrieve swap request details. |
| `SwapRequestsController` | `/api/swap-requests` | `GET /api/swap-requests/incoming` | List incoming swap requests for teacher. |
| `SwapRequestsController` | `/api/swap-requests` | `GET /api/swap-requests/outgoing` | List outgoing swap requests for learner. |
| `SwapRequestsController` | `/api/swap-requests` | `POST /api/swap-requests/{id}/accept` | Accept proposal and provision conversation. |
| `SwapRequestsController` | `/api/swap-requests` | `POST /api/swap-requests/{id}/decline` | Decline proposal. |
| `SwapRequestsController` | `/api/swap-requests` | `POST /api/swap-requests/{id}/cancel` | Cancel outgoing proposal. |
| `AdminWalletsController` | `/api/admin/wallets` | `POST /api/admin/wallets/{userId}/bonus` | Admin idempotent bonus credit adjustment. |
| `SignalR ChatHub` | `/hubs/chat` | WebSocket / Long Polling | Real-time messaging, group join, unread notifications. |

---

## 7. Database Migrations Lead

Developer 1 is the **sole authority** for Entity Framework Core migrations in `src/SkillSwap.Infrastructure/Migrations/*`:
- `20261008143422_InitialCreate`: Core schema, 22 tables, relations, and initial subscription plan seed.
- `20261008154444_HardenPhase3Contracts`: Nullable plan pricing contract hardening.
- `20261010190051_AddCreditTransactionIdempotencyKey`: Append-only credit ledger `IdempotencyKey` column and filtered unique index.
- `ApplicationDbContextModelSnapshot.cs`: Snapshot maintenance.

---

## 8. Developer 1 — Production Deployment & Hosting Credit

**Credited Lead:** [Developer 1 — Islam Khaled](https://github.com/islamkhaled1) (@islamkhaled1)  
**Hosting Provider:** MonsterASP.NET  
**Live Production URL:** `https://skill-swap.runasp.net`  
**Swagger UI:** `https://skill-swap.runasp.net/swagger/index.html`  
**Health Check:** `https://skill-swap.runasp.net/health`  
**Deployment Documentation:** [`docs/deployment/PRODUCTION_DEPLOYMENT.md`](../deployment/PRODUCTION_DEPLOYMENT.md)

Responsibilities executed and maintained:
- Environment variable configuration in MonsterASP.NET Control Panel.
- Secure connection string management using provider-supplied Local Connection String.
- Production startup security validation and JWT key entropy enforcement.
- IIS in-process deployment and application pool lifecycle management.
- Execution and verification of idempotent database migration scripts.

---

## 9. Test Suites Owned

- `tests/SkillSwap.Tests/Unit/AdminWalletsControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/ChatHubTests.cs`
- `tests/SkillSwap.Tests/Unit/ChatServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/SwapRequestsControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/SwapRequestServiceTests.cs`
- `tests/SkillSwap.Tests/Integration/BonusIdempotencyIntegrationTests.cs`
- `tests/SkillSwap.Tests/Integration/HttpFinancialJourneyIntegrationTests.cs`
- `tests/SkillSwap.Tests/Integration/ProductionStartupSecurityIntegrationTests.cs`

---
*Developer 1 Ownership Guide — SkillSwap Backend Engineering Team*
