# SkillSwap Financial & Session Engine Architecture

This document details the core concurrency, authorization, and timing guarantees implemented in the Wallet, Ledger, and Session Booking engine.

---

## 1. Why Monthly Quota is Checked After Wallet Locking

### Problem
When a user books a session, their monthly learning quota (180 minutes for Free, 720 minutes for Premium) is derived dynamically from existing sessions in that calendar month. If quota were calculated before entering the database transaction and before locking the learner's wallet, two parallel booking requests could both observe the same remaining quota and both proceed, exceeding the monthly cap.

### Solution & Flow
Quota evaluation is strictly placed **inside** the transactional boundary **after** acquiring the deterministic row-level update lock (`UPDLOCK, ROWLOCK`) on the learner's wallet:

```
BEGIN TRANSACTION
    ↓
Acquire learner + teacher wallet locks (deterministic UserId order via UPDLOCK)
    ↓
Recalculate monthly learning quota (derives usage from committed sessions)
    ↓
Validate wallet available balance
    ↓
Validate teacher & learner session overlaps
    ↓
Escrow hold learner minutes (Available -= M, Held += M)
    ↓
Insert Session record
    ↓
Insert CreditTransaction (Hold)
    ↓
COMMIT
```

Because the learner's wallet row is exclusively locked, concurrent booking requests for that learner are serialized. When the subsequent transaction acquires the lock, it observes all previously committed sessions in that month and correctly raises `QuotaExceededException`.

---

## 2. Why Completion Requires Participant Authorization

### Problem
Session completion is a financial event that transfers held minutes from escrow to the teacher's available balance (`Learner Held -= M`, `Teacher Available += M`, with `Capture` and `Earn` ledger entries). If the manual completion endpoint only received a session ID without verifying the caller, any authenticated user knowing or guessing a valid session ID could trigger premature financial transfers.

### Solution
Manual session completion (`CompleteSessionAsync(sessionId, requestingUserId)`) explicitly validates that `requestingUserId` matches either `session.TeacherId` or `session.LearnerId`. Unrelated authenticated callers are rejected with `UnauthorizedSessionAccessException`.

---

## 3. Why Manual Completion and System Auto-Completion are Separate

### Problem
Manual completion is an explicit participant-driven action originating from HTTP requests where an authenticated user context is present. Auto-completion is a background maintenance task triggered when a scheduled session has concluded past the configured grace period (`EndUtc + AutoCompletionGracePeriodMinutes <= now`).

### Solution
The system separates these concerns cleanly:
- **`CompleteSessionAsync(sessionId, requestingUserId)`**: Requires authenticated participant identity.
- **`AutoCompleteSessionAsync(sessionId)`**: Internal application operation callable by background schedulers (`SessionAutoCompletionBackgroundService`), requiring no synthetic user identity.

Both operations share the same underlying idempotent, conditional update pipeline:
```sql
UPDATE Sessions
SET Status = Completed, UpdatedAtUtc = @now
WHERE Id = @SessionId AND Status = Scheduled
```
If affected rows equal 0, no duplicate credit transfers occur.

---

## 4. Why Pending Sessions Expire at the Earlier Applicable Boundary

### Problem
A session in `PendingConfirmation` awaits teacher confirmation. If expiration evaluated only `CreatedAtUtc + PendingConfirmationHours` (24 hours), a session booked to start in 2 hours would remain pending even after its scheduled `StartUtc` had passed without confirmation, keeping learner credits locked in escrow.

### Solution
Pending sessions expire at the **earlier applicable point**:
1. `CreatedAtUtc + PendingConfirmationHours <= now` (24 hours elapsed without teacher confirmation)
   **OR**
2. `StartUtc <= now` (scheduled start time has arrived without confirmation)

On expiration:
- `Status` transitions conditionally from `PendingConfirmation` to `Expired`.
- Learner escrowed minutes are restored (`Held -= M`, `Available += M`).
- Exactly one `Release` ledger entry is appended.
- Learning quota is released (not consumed).
- Repeated background runs are strictly idempotent.

---

## 5. SwapRequest Role Contract & Session Mapping

### Explicit Role Contract
- **`SwapRequest.RequesterId`** = Learner (User requesting to learn a skill)
- **`SwapRequest.ReceiverId`** = Teacher (User offering the skill)
- **`SwapRequest.SkillId`** = The specific skill to be learned

### Session Creation Rules
When scheduling a session from an accepted `SwapRequest`:
- `Session.LearnerId` MUST equal `SwapRequest.RequesterId`.
- `Session.TeacherId` MUST equal `SwapRequest.ReceiverId`.
- `Session.SkillId` MUST equal `SwapRequest.SkillId`.
- The Receiver must have an active `UserSkill` record with `Type = Teach` for `SwapRequest.SkillId`.
- Attempts to reverse roles (e.g. the receiver attempting to book as the learner) are strictly rejected with `UnauthorizedSessionAccessException`.

---

## 6. No-Show Reporting Authorization

### Reporting Rules
- Only participants of the session (`TeacherId` or `LearnerId`) are authorized to report a no-show.
- A participant may **only** report the **other** party as a no-show:
  - Caller = `LearnerId` -> May only submit `NoShowParty.Teacher`.
  - Caller = `TeacherId` -> May only submit `NoShowParty.Learner`.
- An individual participant cannot report `NoShowParty.Both`.
- Unrelated callers are rejected with `UnauthorizedSessionAccessException`.
- Administrative workflows or automated system arbitration may submit `NoShowParty.Both` via administrative authorization (`isAdmin = true`).

---

## 7. Dispute Resolution Administrative Boundary

### Dispute Handling
- Participants can report disputes during a session (`ReportDisputeAsync`), freezing credits in escrow.
- Dispute resolution (`ResolveDisputeAsync`) is strictly restricted to platform administrators:
  - API endpoint: `POST api/admin/sessions/{id}/resolve-dispute` guarded by `[Authorize(Roles = "Admin")]`.
  - Defense in depth: The service explicitly rejects self-arbitration (`adminId == session.TeacherId || adminId == session.LearnerId`).

---

## 8. Premium Plan Pricing Status

### Monetization Policy
- In the initial MVP, billing and payment gateway integrations are intentionally postponed.
- To prevent hardcoding unapproved business assumptions (e.g., guessed prices like 9.99):
  - `SubscriptionPlan.Price` is defined as nullable (`decimal?`).
  - `FREE` is seeded with `Price = 0.00`.
  - `PREMIUM` is seeded with `Price = NULL`, explicitly marking pricing as not yet finalized.
