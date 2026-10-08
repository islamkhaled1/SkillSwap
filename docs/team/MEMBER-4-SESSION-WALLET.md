# Member 4 -- Session & Wallet (Lead Financial & Migration Owner)

## 1. Mission
Serve as the platform's core financial and database authority. Own the time-banking ledger, wallet balances, escrow lifecycle, session booking and scheduling engine, double-completion protection, monthly quota enforcement, no-show and dispute arbitration backend, background maintenance workers, and act as the **sole authority for Entity Framework Core migrations**.

---

## 2. Scope of Ownership
- **Financial Engine:** Wallet balance state (`AvailableMinutes`, `HeldMinutes`), append-only credit ledger (`CreditTransaction`), ledger reconciliation, and SQL Server row-level locking (`SqlWalletLockService`).
- **Session Booking & Lifecycle:** Booking validation, teacher confirmation, participant join tracking, session completion, cancellation, no-show reporting, and dispute escalation.
- **Background Jobs:** Automated session completion worker (`SessionAutoCompletionBackgroundService`) and pending session expiration worker (`PendingSessionExpirationBackgroundService`).
- **Administrative Arbitration:** Dispute resolution endpoint restricted to platform administrators.
- **Database Migrations:** Single exclusive owner of `src/SkillSwap.Infrastructure/Migrations/*` and `ApplicationDbContextModelSnapshot.cs`.

---

## 3. Responsibilities
- Ensure wallet balances (`AvailableMinutes`, `HeldMinutes`) can **never** become negative under any circumstance.
- Guarantee that all financial state transitions append corresponding audit records to `CreditTransactions` inside the same database transaction.
- Prevent booking race conditions by acquiring deterministic row-level locks on wallets ordered by `UserId` (`MIN(LearnerId, TeacherId)` then `MAX(LearnerId, TeacherId)`) using `UPDLOCK, ROWLOCK`.
- Enforce monthly learning quota checks (Free: 180 min, Premium: 720 min) strictly **inside** the transactional boundary after acquiring the wallet lock.
- Prevent double-completion race conditions via atomic conditional SQL updates (`WHERE Status = Scheduled`).
- Maintain background jobs to guarantee pending sessions expire at the earlier of 24h or scheduled `StartUtc`, and scheduled sessions auto-complete after `EndUtc + GracePeriod`.
- Review, validate, and execute all database migrations for all 5 team members.

---

## 4. Domain Entities Owned

| Entity | Primary Key | Table | Description |
|---|---|---|---|
| `Session` | `long` | `Sessions` | Scheduled session record tracking teacher, learner, skill, start/end UTC, status, dispute, and no-show flags. |
| `Wallet` | `Guid` | `Wallets` | Materialized balance record holding `AvailableMinutes` and `HeldMinutes` for a user. |
| `CreditTransaction` | `long` | `CreditTransactions` | Immutable, append-only ledger transaction recording deltas and audit descriptions. |

### Enums Owned:
- `CreditTransactionType` (`Bonus = 1`, `Hold = 2`, `Release = 3`, `Capture = 4`, `Earn = 5`, `Adjustment = 6`).
- `NoShowParty` (`Learner = 1`, `Teacher = 2`, `Both = 3`).
- `SessionMode` (`Online = 1`, `InPerson = 2`).
- `SessionStatus` (`PendingConfirmation = 1`, `Scheduled = 2`, `Completed = 3`, `Cancelled = 4`, `NoShow = 5`, `Disputed = 6`, `Expired = 7`).

### Domain Exceptions Owned:
- `InsufficientBalanceException` (`src/SkillSwap.Domain/Exceptions/InsufficientBalanceException.cs`)
- `InvalidSessionStateException` (`src/SkillSwap.Domain/Exceptions/InvalidSessionStateException.cs`)
- `QuotaExceededException` (`src/SkillSwap.Domain/Exceptions/QuotaExceededException.cs`)
- `SessionOverlapException` (`src/SkillSwap.Domain/Exceptions/SessionOverlapException.cs`)
- `UnauthorizedSessionAccessException` (`src/SkillSwap.Domain/Exceptions/UnauthorizedSessionAccessException.cs`)

---

## 5. Application Services Owned

| Interface / Class | Location | Status | Description |
|---|---|---|---|
| `IWalletService` | `src/SkillSwap.Application/Abstractions/IWalletService.cs` | **IMPLEMENTED** | Wallet queries, balance transfers, and credit adjustments. |
| `WalletService` | `src/SkillSwap.Application/Services/WalletService.cs` | **IMPLEMENTED** | Implements non-negative wallet mutation invariants. |
| `ICreditLedgerService` | `src/SkillSwap.Application/Abstractions/ICreditLedgerService.cs` | **IMPLEMENTED** | Appends and retrieves ledger audit trail entries. |
| `CreditLedgerService` | `src/SkillSwap.Application/Services/CreditLedgerService.cs` | **IMPLEMENTED** | Implements append-only ledger operations. |
| `IQuotaService` | `src/SkillSwap.Application/Abstractions/IQuotaService.cs` | **IMPLEMENTED** | Derives monthly learning usage from committed sessions. |
| `QuotaService` | `src/SkillSwap.Application/Services/QuotaService.cs` | **IMPLEMENTED** | Implements 180m/720m calendar-month cap calculation. |
| `ISessionBookingService` | `src/SkillSwap.Application/Abstractions/ISessionBookingService.cs` | **IMPLEMENTED** | Orchestrates transactional session booking with row locking. |
| `SessionBookingService` | `src/SkillSwap.Application/Services/SessionBookingService.cs` | **IMPLEMENTED** | Implements lock-order, quota-check, escrow-hold pipeline. |
| `ISessionService` | `src/SkillSwap.Application/Abstractions/ISessionService.cs` | **IMPLEMENTED** | Manages session queries, joins, cancellations, no-shows, disputes. |
| `SessionService` | `src/SkillSwap.Application/Services/SessionService.cs` | **IMPLEMENTED** | Implements participant authorization and state transitions. |
| `ISessionCompletionService` | `src/SkillSwap.Application/Abstractions/ISessionCompletionService.cs` | **IMPLEMENTED** | Handles manual and auto-completion credit escrow settlement. |
| `SessionCompletionService` | `src/SkillSwap.Application/Services/SessionCompletionService.cs` | **IMPLEMENTED** | Implements atomic conditional SQL update protection. |
| `ILedgerReconciliationService` | `src/SkillSwap.Application/Abstractions/ILedgerReconciliationService.cs` | **IMPLEMENTED** | Audits wallet balances against summed ledger transactions. |
| `LedgerReconciliationService` | `src/SkillSwap.Application/Services/LedgerReconciliationService.cs` | **IMPLEMENTED** | Reconciles discrepancies between wallets and ledgers. |
| `IWalletLockService` | `src/SkillSwap.Application/Abstractions/IWalletLockService.cs` | **IMPLEMENTED** | Deterministic SQL Server row locking abstraction. |
| `SqlWalletLockService` | `src/SkillSwap.Infrastructure/Services/SqlWalletLockService.cs` | **IMPLEMENTED** | Executes raw SQL UPDLOCK row queries in deterministic order. |

---

## 6. Controllers / API Areas Owned

| Controller | Route Area | Status | Description |
|---|---|---|---|
| `WalletsController` | `/api/wallets` | **IMPLEMENTED** | Authenticated wallet balance, transaction ledger, and quota queries. |
| `SessionsController` | `/api/sessions` | **IMPLEMENTED** | Session booking, queries, confirmations, joins, completes, cancels, no-shows, disputes. |
| `AdminSessionsController` | `/api/admin/sessions` | **IMPLEMENTED** | Admin-only dispute arbitration endpoint. |

---

## 7. Files I Own

### Entities & Configurations:
- `src/SkillSwap.Domain/Entities/Session.cs`
- `src/SkillSwap.Domain/Entities/Wallet.cs`
- `src/SkillSwap.Domain/Entities/CreditTransaction.cs`
- `src/SkillSwap.Domain/Enums/CreditTransactionType.cs`
- `src/SkillSwap.Domain/Enums/NoShowParty.cs`
- `src/SkillSwap.Domain/Enums/SessionMode.cs`
- `src/SkillSwap.Domain/Enums/SessionStatus.cs`
- `src/SkillSwap.Domain/Exceptions/*`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/SessionConfiguration.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/WalletConfiguration.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/CreditTransactionConfiguration.cs`

### Application Services & DTOs:
- All files in `src/SkillSwap.Application/Services/`
- All files in `src/SkillSwap.Application/DTOs/Sessions/`
- All files in `src/SkillSwap.Application/DTOs/Wallets/`
- `src/SkillSwap.Application/Common/Options/SessionPolicyOptions.cs`

### Background Hosted Services & Infrastructure:
- `src/SkillSwap.Infrastructure/BackgroundJobs/SessionAutoCompletionBackgroundService.cs`
- `src/SkillSwap.Infrastructure/BackgroundJobs/PendingSessionExpirationBackgroundService.cs`
- `src/SkillSwap.Infrastructure/Services/SqlWalletLockService.cs`
- `src/SkillSwap.Infrastructure/Services/DateTimeProvider.cs`
- `src/SkillSwap.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/SkillSwap.Infrastructure/Migrations/*` (Exclusive Owner)

### API Controllers:
- `src/SkillSwap.API/Controllers/WalletsController.cs`
- `src/SkillSwap.API/Controllers/SessionsController.cs`
- `src/SkillSwap.API/Controllers/AdminSessionsController.cs`

---

## 8. Files I Must Not Modify Without Coordination

- `src/SkillSwap.Infrastructure/Identity/ApplicationUser.cs` (Owner: Member 1)
- `src/SkillSwap.Domain/Entities/UserProfile.cs` (Owner: Member 1)
- `src/SkillSwap.Domain/Entities/Category.cs` & `Skill.cs` (Owner: Member 2)
- `src/SkillSwap.Domain/Entities/SwapRequest.cs` (Owner: Member 3)
- `src/SkillSwap.Domain/Entities/Review.cs` & `SubscriptionPlan.cs` (Owner: Member 5)

---

## 9. Dependencies
- **Consumes:**
  - `ICurrentUserService` (Member 1) to identify callers.
  - `SwapRequest` (Member 3) to validate booking authorization and skill match.
  - `UserSkill` (Member 2) to verify teacher qualification.
  - `UserSubscription` (Member 5) to determine monthly learning quota limit (180 vs 720 mins).
- **Supplies Services to:**
  - Member 1 (wallet creation on registration).
  - Member 5 (session validation for eligible reviews).

---

## 10. Cross-Module Contracts

1. **Session Mapping from SwapRequest:**
   - When booking a session from a `SwapRequest`:
     - `Session.LearnerId` MUST equal `SwapRequest.RequesterId`.
     - `Session.TeacherId` MUST equal `SwapRequest.ReceiverId`.
     - `Session.SkillId` MUST equal `SwapRequest.SkillId`.
     - Role reversal attempts are rejected with `UnauthorizedSessionAccessException`.
2. **Review Eligibility Contract (Member 5):**
   - A user can only submit a review for a session if `Session.Status == SessionStatus.Completed` and the reviewer is a verified participant.
3. **Registration Initial Wallet (Member 1):**
   - New user registration must initialize a `Wallet` with `AvailableMinutes = 0` and `HeldMinutes = 0`.

---

## 11. Business Rules I Must Respect

1. **Time Currency:** 1 credit = 30 minutes. All stored values are integer minutes.
2. **Non-Negative Balances:** `AvailableMinutes >= 0` and `HeldMinutes >= 0` at all times.
3. **Escrow Booking:** Available -= M, Held += M, ledger type `Hold`.
4. **Session Durations:** Exactly 30, 60, or 90 minutes. Reject any other duration.
5. **Session Completion:** Held -= M, Teacher Available += M. 1:1 transfer. 0 platform fee.
6. **No-Show Arbitration:**
   - Learner no-show: Captured for teacher (`Capture` + `Earn`). Quota consumed.
   - Teacher no-show: Released back to learner (`Release`). Quota released.
   - Both no-show: Released back to learner (`Release`).
   - Reporting: Learner reports Teacher only; Teacher reports Learner only; participants cannot self-report or report "Both". Admin can mark "Both".
7. **Dispute Resolution:** Strictly Admin-only (`POST /api/admin/sessions/{id}/resolve-dispute`). Credits frozen in escrow until resolved. No participant self-arbitration.
8. **Pending Expiration:** Earlier of: `CreatedAtUtc + PendingConfirmationHours` OR `StartUtc <= UtcNow`.
9. **Auto-Completion:** Concluded sessions past `EndUtc + GracePeriod` auto-complete regardless of join timestamps.
10. **Financial Immutability:** Financial records are NEVER physically deleted (`DeleteBehavior.Restrict`).

---

## 12. Planned Endpoints
*None. All core financial and session endpoints are already implemented and tested.*

---

## 13. Existing Implemented Endpoints

### WalletsController (`api/wallets`):
- `GET /api/wallets/me` (**IMPLEMENTED**) -- Returns current user's available and held balances.
- `GET /api/wallets/me/transactions` (**IMPLEMENTED**) -- Returns credit ledger history with pagination.
- `GET /api/wallets/me/quota` (**IMPLEMENTED**) -- Returns monthly used and remaining learning quota.

### SessionsController (`api/sessions`):
- `POST /api/sessions/book` (**IMPLEMENTED**) -- Books session, acquires wallet row locks, holds escrow.
- `GET /api/sessions/{id}` (**IMPLEMENTED**) -- Retrieves session details (participant authorized).
- `GET /api/sessions` (**IMPLEMENTED**) -- Lists sessions for caller filtered by status.
- `POST /api/sessions/{id}/confirm` (**IMPLEMENTED**) -- Teacher confirms pending session.
- `POST /api/sessions/{id}/cancel` (**IMPLEMENTED**) -- Cancels session, releases escrow hold.
- `POST /api/sessions/{id}/join` (**IMPLEMENTED**) -- Marks participant join timestamp.
- `POST /api/sessions/{id}/complete` (**IMPLEMENTED**) -- Completes session, transfers escrow to teacher.
- `POST /api/sessions/{id}/no-show` (**IMPLEMENTED**) -- Reports no-show for other party.
- `POST /api/sessions/{id}/dispute` (**IMPLEMENTED**) -- Reports dispute, freezes escrow.

### AdminSessionsController (`api/admin/sessions`):
- `POST /api/admin/sessions/{id}/resolve-dispute` (**IMPLEMENTED**) -- Admin arbitrates dispute funds.

---

## 14. Events / SignalR / Notifications
- Triggers notifications via Member 5 for session confirmation, cancellation, reminders, and completions.

---

## 15. Database Responsibility & Migration Authority
- **Single Migration Authority:** Sole owner of `src/SkillSwap.Infrastructure/Migrations/*`.
- Ensures foreign keys on `Sessions`, `Wallets`, and `CreditTransactions` use `DeleteBehavior.Restrict` or `NoAction` to eliminate multiple cascade paths in SQL Server.
- Validates that SQL Server row-level locking hints (`UPDLOCK, ROWLOCK`) operate efficiently.

---

## 16. Validation & Authorization
- `BookSessionRequest`: Duration strictly 30, 60, or 90. `StartUtc` must be in the future.
- Strict participant validation on all session operations.
- Role-based authorization (`[Authorize(Roles = "Admin")]`) on dispute resolution.

---

## 17. Testing Requirements
- **Concurrency Integration Tests:** Real SQL Server concurrency tests verifying that parallel bookings for the same user serialize properly without quota overruns or deadlocks.
- **Unit Tests:**
  - Invariant checks for negative balances.
  - Double-completion protection verification.
  - No-show reporting authorization tests.
  - Dispute resolution authorization tests.

---

## 18. Definition of Done
- [ ] Financial mutations wrapped in database transactions.
- [ ] Wallet balances validated against negative amounts.
- [ ] Append-only ledger updated atomically with every balance change.
- [ ] Idempotent double-completion conditional update verified.
- [ ] Deterministic row locking in `MIN/MAX(UserId)` order verified.
- [ ] Monthly learning quota evaluated after row locking.
- [ ] EF Core migrations validated and cleanly applied.
- [ ] 100% test pass rate on `dotnet test` (all 74+ tests passing).

---

## 19. PR Checklist
- [ ] Does `dotnet test` pass with 0 errors?
- [ ] Are financial records immutable without physical deletes?
- [ ] Is row-level lock ordering deterministic?
- [ ] Is the migration snapshot up to date?
- [ ] Does the branch follow `feature/session-wallet`?

---

## 20. Common Mistakes to Avoid
- **Mistake 1:** Checking monthly learning quota before acquiring wallet row locks. (Leads to race condition quota overruns).
- **Mistake 2:** Allowing non-participants to complete sessions or report no-shows.
- **Mistake 3:** Allowing participants to report themselves or report "Both" as no-show. (Only admins or system can mark "Both").
- **Mistake 4:** Generating competing EF Core migrations. (Member 4 is the sole migration creator).

---
*Member 4 Ownership Guide -- SkillSwap Backend Team*
