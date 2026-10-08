# Business Rules

> This document is the authoritative source for all SkillSwap business rules.
> All agents and developers MUST read this before implementing any feature that touches wallet, sessions, or credits.

---

## 1. Time Is the Currency

SkillSwap uses **time minutes** as the unit of exchange.

| Conceptual Credit | Stored Value (INTEGER minutes) |
|---|---|
| 1 credit | 30 minutes |
| 2 credits | 60 minutes |
| 3 credits | 90 minutes |

**Database stores INTEGER MINUTES only.** No decimal credits are stored in the core wallet logic.

---

## 2. Wallet Structure

Each user has exactly one Wallet with:

| Field | Type | Rule |
|---|---|---|
| `AvailableMinutes` | int | Current spendable balance. NEVER negative. |
| `HeldMinutes` | int | Minutes reserved for scheduled sessions. NEVER negative. |

**Invariant: `AvailableMinutes >= 0` and `HeldMinutes >= 0` at all times.**

---

## 3. Booking / Hold

When a learner books a session (status becomes `Scheduled`):

```
AvailableMinutes -= sessionDurationMinutes
HeldMinutes      += sessionDurationMinutes
```

The minutes are NOT permanently spent yet—they are held in escrow.

---

## 4. Session Completion

When a session is successfully completed (status transitions `Scheduled` -> `Completed`):

**Learner:**
```
HeldMinutes -= sessionDurationMinutes
```

**Teacher:**
```
AvailableMinutes += sessionDurationMinutes
```

No platform fee in MVP.

---

## 5. Cancellation

### Early learner cancellation:
```
Learner.HeldMinutes      -= sessionDurationMinutes
Learner.AvailableMinutes += sessionDurationMinutes
Monthly learning usage    released
```

### Teacher cancellation:
```
Learner.HeldMinutes      -= sessionDurationMinutes
Learner.AvailableMinutes += sessionDurationMinutes
Monthly learning usage    released
Teacher reliability strike (if repeated)
```

> Credit penalties for late cancellation are postponed from MVP.

---

## 6. No-Show

### Reporting Authorization Rules:
- Only session participants may report a no-show.
- A participant may only report the OTHER party as a no-show:
  - A Learner may only report Teacher as a no-show.
  - A Teacher may only report Learner as a no-show.
  - An individual participant cannot report "Both".
- Unrelated callers are rejected with `UnauthorizedSessionAccessException`.
- Administrative workflows or system policies can mark "Both" (or any party) upon investigation.

### Learner no-show:
```
Learner.HeldMinutes      -= sessionDurationMinutes  (captured, not returned)
Teacher.AvailableMinutes += sessionDurationMinutes
Monthly quota REMAINS consumed
```

### Teacher no-show:
```
Learner.HeldMinutes      -= sessionDurationMinutes
Learner.AvailableMinutes += sessionDurationMinutes  (returned)
Monthly usage released
```

### Both no-show:
```
Learner.HeldMinutes      -= sessionDurationMinutes
Learner.AvailableMinutes += sessionDurationMinutes  (returned)
```

---

## 7. Disputed Session

A disputed session has status `Disputed`:
- Credit transfer is **frozen**
- Admin/manual resolution required (`[Authorize(Roles = "Admin")]` on `api/admin/sessions/{id}/resolve-dispute`)
- Participants cannot arbitrate their own disputes.
- Dispute metadata: `ReportedById`, `ResolvedAtUtc`, `ResolutionNote`

---

## 8. Session Durations

Allowed session duration values (minutes):

| Value | Meaning |
|---|---|
| 30 | 30 minutes |
| 60 | 60 minutes |
| 90 | 90 minutes |

**`InProgress` is NOT a stored status.** It is derived from current UTC time:
```
session.StartUtc <= UtcNow <= session.EndUtc  =>  effectively InProgress
```

---

## 9. Monthly Learning Quota

Monthly quota is separate from wallet balance.

| Plan | Monthly Learning Cap |
|---|---|
| Free | 180 minutes/month |
| Premium | 720 minutes/month |

- Teaching is **NOT** capped by this quota.
- Month boundary is determined by `Session.StartUtc` month.
- **No `MonthlyUsage` table in MVP.** Usage is derived from sessions.

---

## 10. Subscription Plans

| Plan | Default | Monthly Quota | Notes |
|---|---|---|---|
| Free | Yes | 180 min | Auto-assigned at registration |
| Premium | No | 720 min | Real payment postponed; mock allowed in MVP |

A `UserSubscription` (status = Active, plan = Free) record should be created at registration.

---

## 11. Matching

Matching is **calculated dynamically** (no Match entity). User skill data drives recommendations.

---

## 12. Swap Request Lifecycle

```
Pending -> Accepted | Declined | Cancelled | Expired
```

- **No `Completed` status** on SwapRequest.
- One accepted SwapRequest can lead to multiple Sessions.
- Old pending requests expire after **~7 days**.

---

## 13. Session ↔ SwapRequest Integrity

- **Strict Role Contract:**
  - `SwapRequest.RequesterId` = Learner
  - `SwapRequest.ReceiverId` = Teacher
  - `Session.LearnerId` must strictly equal `SwapRequest.RequesterId`.
  - `Session.TeacherId` must strictly equal `SwapRequest.ReceiverId`.
  - Attempts to reverse roles (e.g. receiver attempting to book as learner) are rejected.
- `Session.SkillId` must match `SwapRequest.SkillId`.
- Teacher (`ReceiverId`) must actively teach that skill (`UserSkill.Type = Teaching` or `Teach`).

---

## 14. Session Mode

| Mode | MVP |
|---|---|
| Online | ✅ Supported |
| InPerson | 🔮 Future-ready |

---

## 15. Booking Concurrency

Booking must be **transaction-safe**.

Wallet rows must be locked in **deterministic user-ID order** using SQL Server `UPDLOCK` hints to:
- Prevent double-booking
- Prevent insufficient balance race conditions
- Reduce deadlocks

Lock order: `MIN(LearnerId, TeacherId)` first, then `MAX(LearnerId, TeacherId)`.

---

## 16. Financial / Session Record Deletion

Session, wallet, credit ledger, and financial history records **MUST NOT be physically deleted**.

EF Core relationships for these entities must use:
- `DeleteBehavior.Restrict`, or
- `DeleteBehavior.NoAction`

where cascades would violate integrity.

---

## 17. Credit Ledger (CreditTransaction)

The credit ledger is **append-only**. Supported transaction types:

| Type | When |
|---|---|
| `Bonus` | Welcome bonus, promotion |
| `Hold` | Session booking |
| `Release` | Cancellation, no-show (teacher) |
| `Capture` | Learner minutes captured (no-show/completion) |
| `Earn` | Teacher receives minutes on completion |
| `Adjustment` | Admin manual correction |

Wallet balances are the current **materialized state**. Ledger is the audit trail.
Ledger entries must be written **atomically** with wallet state changes (same transaction).

---

## 18. Double Completion Protection

Session completion updates must be conditional:

```sql
UPDATE Sessions SET Status = 'Completed' WHERE Id = @id AND Status = 'Scheduled'
```

This prevents race conditions where two requests complete the same session simultaneously.

---

## 19. Pending Session Expiration

Sessions awaiting teacher confirmation expire after **~24 hours**.

On expiry:
- Session status changes appropriately (e.g., `Expired`)
- Held minutes released back to learner

---

## 20. Old Swap Request Expiration

Pending swap requests expire after **~7 days** with no response.

---

## 21. Background Jobs (Planned)

> Do NOT implement yet. Document only.

| Job | Trigger | Action |
|---|---|---|
| Auto-complete session | `EndUtc + grace period` | Set `Completed`, transfer credits |
| Expire pending sessions | Scheduled | Expire sessions after 24h, release hold |
| Expire old swap requests | Scheduled | Mark as `Expired` after 7 days |

Each background operation must be **transactional** and use **conditional status updates**.

---

## Summary of Invariants

1. Wallet balance is never negative
2. HeldMinutes is never negative
3. Session completion is idempotent (conditional update)
4. Ledger entries are written atomically with wallet changes
5. Session.SkillId always matches SwapRequest.SkillId
6. Monthly quota uses Session.StartUtc month
7. No physical deletes on financial records

