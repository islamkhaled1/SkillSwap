# Member 5 -- Platform Features

## 1. Mission
Own cross-cutting user engagement, social safety, and monetization capability infrastructure for SkillSwap. Deliver post-session reviews and ratings, user notifications, milestone badges, favorites, user blocking, moderation reports, and subscription tier capabilities.

---

## 2. Scope of Ownership
- **Post-Session Reviews:** Ratings (1 to 5 stars) and comments on completed sessions.
- **Notifications:** In-app notification center tracking delivery, read receipts, and notification kinds.
- **Gamification Badges:** Master badge catalog and user badge awards (`Badge`, `UserBadge`).
- **Social & Safety:** Favorite users list (`Favorite`), user blocking (`UserBlock`), and moderation reporting (`Report`).
- **Subscriptions & Plans:** Subscription tier models (`SubscriptionPlan`, `UserSubscription`) providing learning caps (180 min Free vs 720 min Premium) and capability flags.
- **Monetization Invariant:** **Payment gateways (Stripe, PayPal) are strictly out of MVP scope.** Premium pricing is **not finalized** (`SubscriptionPlan.Price = NULL`).

---

## 3. Responsibilities
- Implement review submission (`POST /api/reviews`) and user review history (`GET /api/users/{id}/reviews`).
- Implement user notification listing (`GET /api/notifications`) and read receipt marking (`POST /api/notifications/{id}/read`).
- Implement badge listing (`GET /api/badges`) and user badges (`GET /api/users/{id}/badges`).
- Implement favorite user toggles (`GET /api/favorites`, `POST /api/favorites/{userId}`, `DELETE /api/favorites/{userId}`).
- Implement user blocking and unblocking (`POST /api/blocks/{userId}`, `DELETE /api/blocks/{userId}`).
- Implement moderation reporting (`POST /api/reports`).
- Implement subscription catalog and mock subscription management (`GET /api/subscriptions/plans`, `GET /api/subscriptions/me`, `POST /api/subscriptions/subscribe`).
- Provide fast query abstractions for checking user blocks and active subscription caps.
- Write unit tests for rating validations, block integrity, and review eligibility.

---

## 4. Domain Entities Owned

| Entity | Primary Key | Table | Description |
|---|---|---|---|
| `Review` | `long` | `Reviews` | Feedback rating and comment left by a participant on a completed session. |
| `Notification` | `long` | `Notifications` | User alert record with notification kind, payload, and read timestamp. |
| `Badge` | `int` | `Badges` | Platform achievement definition (Code, Name, Description, IconUrl). |
| `UserBadge` | `(Guid, int)` | `UserBadges` | Associative record of a badge awarded to a user. |
| `Favorite` | `(Guid, Guid)` | `Favorites` | User bookmarking another user. |
| `UserBlock` | `(Guid, Guid)` | `UserBlocks` | User blocking another user from swaps and messaging. |
| `Report` | `long` | `Reports` | User-submitted moderation issue regarding conduct or sessions. |
| `SubscriptionPlan` | `int` | `SubscriptionPlans` | Tier definition (Free vs Premium) with learning limits and features. |
| `UserSubscription` | `long` | `UserSubscriptions` | User subscription enrollment record with status and validity dates. |

### Enums Owned:
- `NotificationKind` (`SwapRequest = 1`, `SessionReminder = 2`, `SessionCompleted = 3`, `SessionCancelled = 4`, `NewMessage = 5`, `ReviewReceived = 6`, `BadgeEarned = 7`, `System = 8`).
- `ReportReason` (`Harassment = 1`, `InappropriateContent = 2`, `Spam = 3`, `NoShow = 4`, `Fraud = 5`, `Other = 6`).
- `ReportStatus` (`Open = 1`, `UnderReview = 2`, `Resolved = 3`, `Rejected = 4`).
- `SubscriptionPlanType` (`Free = 1`, `Premium = 2`).
- `SubscriptionStatus` (`Active = 1`, `Cancelled = 2`, `Expired = 3`).

---

## 5. Application Services Owned

| Interface / Class | Location | Status | Description |
|---|---|---|---|
| `IReviewService` | `src/SkillSwap.Application/Abstractions/IReviewService.cs` | **PLANNED** | Manages session reviews, rating validation, and public feedback. |
| `INotificationService` | `src/SkillSwap.Application/Abstractions/INotificationService.cs` | **PLANNED** | Dispatches, queries, and marks user notifications. |
| `IBadgeService` | `src/SkillSwap.Application/Abstractions/IBadgeService.cs` | **PLANNED** | Queries badges and awards achievements. |
| `IFavoriteService` | `src/SkillSwap.Application/Abstractions/IFavoriteService.cs` | **PLANNED** | Manages user favorite bookmarks. |
| `IBlockService` | `src/SkillSwap.Application/Abstractions/IBlockService.cs` | **PLANNED** | Manages user blocks and validates communication permissions. |
| `IReportService` | `src/SkillSwap.Application/Abstractions/IReportService.cs` | **PLANNED** | Submits and tracks user moderation reports. |
| `ISubscriptionService` | `src/SkillSwap.Application/Abstractions/ISubscriptionService.cs` | **PLANNED** | Queries plans and manages active user subscription status. |

---

## 6. Controllers / API Areas Owned

| Controller | Route Area | Status | Description |
|---|---|---|---|
| `ReviewsController` | `/api/reviews` | **PLANNED** | Submit reviews and query feedback for users. |
| `NotificationsController` | `/api/notifications` | **PLANNED** | List user alerts and mark as read. |
| `BadgesController` | `/api/badges` | **PLANNED** | List catalog badges and user-earned badges. |
| `FavoritesController` | `/api/favorites` | **PLANNED** | Add, remove, and list user favorites. |
| `BlocksController` | `/api/blocks` | **PLANNED** | Block and unblock users. |
| `ReportsController` | `/api/reports` | **PLANNED** | Submit moderation reports. |
| `SubscriptionsController` | `/api/subscriptions` | **PLANNED** | Query plans and manage user subscription tier. |

---

## 7. Files I Own

### Entities & Configurations:
- `src/SkillSwap.Domain/Entities/Review.cs`
- `src/SkillSwap.Domain/Entities/Notification.cs`
- `src/SkillSwap.Domain/Entities/Badge.cs`
- `src/SkillSwap.Domain/Entities/UserBadge.cs`
- `src/SkillSwap.Domain/Entities/Favorite.cs`
- `src/SkillSwap.Domain/Entities/UserBlock.cs`
- `src/SkillSwap.Domain/Entities/Report.cs`
- `src/SkillSwap.Domain/Entities/SubscriptionPlan.cs`
- `src/SkillSwap.Domain/Entities/UserSubscription.cs`
- All matching configurations in `src/SkillSwap.Infrastructure/Persistence/Configurations/`

### Planned DTOs & Services:
- `src/SkillSwap.Application/DTOs/Reviews/*`
- `src/SkillSwap.Application/DTOs/Notifications/*`
- `src/SkillSwap.Application/DTOs/Badges/*`
- `src/SkillSwap.Application/DTOs/Favorites/*`
- `src/SkillSwap.Application/DTOs/Blocks/*`
- `src/SkillSwap.Application/DTOs/Reports/*`
- `src/SkillSwap.Application/DTOs/Subscriptions/*`
- Matching services under `src/SkillSwap.Application/Services/`

---

## 8. Files I Must Not Modify Without Coordination

- `src/SkillSwap.Domain/Entities/Session.cs` (Owner: Member 4)
- `src/SkillSwap.Domain/Entities/Wallet.cs` (Owner: Member 4)
- `src/SkillSwap.Domain/Entities/CreditTransaction.cs` (Owner: Member 4)
- `src/SkillSwap.Application/Services/WalletService.cs` (Owner: Member 4)
- `src/SkillSwap.Application/Services/SessionBookingService.cs` (Owner: Member 4)
- `src/SkillSwap.Infrastructure/Persistence/ApplicationDbContext.cs` (Owner: Member 4)
- `src/SkillSwap.Infrastructure/Migrations/*` (Owner: Member 4 ONLY)

---

## 9. Dependencies
- **Consumes:**
  - `ICurrentUserService` (Member 1) to identify callers.
  - `Session` from Member 4 to verify completion before allowing reviews.
- **Supplies to:**
  - Member 4 (`QuotaService` queries `UserSubscription` to determine 180 min vs 720 min cap).
  - Member 3 (`SwapRequest` and `ChatHub` query `UserBlocks` to prevent blocked interactions).

---

## 10. Cross-Module Contracts

1. **Review Eligibility Invariant:**
   - A review may only be submitted if:
     - `Session.Status == SessionStatus.Completed`.
     - `ReviewerId` is either `Session.LearnerId` or `Session.TeacherId`.
     - `RevieweeId` is the other participant in that session.
     - No prior review has been submitted by that reviewer for that session.
2. **Subscription Tier Limits Contract (Member 4):**
   - Active Free plan: `MonthlyLearningMinutes = 180`.
   - Active Premium plan: `MonthlyLearningMinutes = 720`.
   - If no active subscription record is found, fallback to default Free limit (180 minutes).
3. **Safety / Block Enforcement Contract (Member 3):**
   - If `UserBlock` exists between User A and User B (in either direction), SwapRequests and Messages between them are rejected.

---

## 11. Business Rules I Must Respect

1. **Review Ratings:** `Rating` must be an integer between 1 and 5 inclusive.
2. **Dynamic Rating Aggregation:** Average ratings and review counts must be computed dynamically via database queries. Never attempt to cache or persist averages onto `UserProfile`.
3. **Unresolved Premium Pricing:**
   - `SubscriptionPlan.Price` is defined as nullable (`decimal?`).
   - `FREE` plan is seeded with `Price = 0.00`.
   - `PREMIUM` plan is seeded with `Price = NULL`.
   - **Never hardcode assumptions like 9.99.**
4. **No Real Payment Gateways in MVP:**
   - Real payment processing (Stripe, PayPal) is postponed.
   - Any upgrade to Premium is mock/simulated in MVP.
5. **No Self-Interactions:**
   - Cannot review yourself.
   - Cannot favorite yourself (`UserId != FavoriteUserId`).
   - Cannot block yourself (`BlockerId != BlockedId`).

---

## 12. Planned Endpoints

| Method | Route | Description | Auth Required | Role |
|---|---|---|---|---|
| `POST` | `/api/reviews` | Submit feedback review for a completed session | Yes | Authenticated |
| `GET` | `/api/users/{id}/reviews` | List public reviews received by a user | No | Anonymous |
| `GET` | `/api/notifications` | List notifications for authenticated user | Yes | Authenticated |
| `POST` | `/api/notifications/{id}/read` | Mark a notification as read | Yes | Authenticated |
| `GET` | `/api/badges` | List all available system badges | No | Anonymous |
| `GET` | `/api/users/{id}/badges` | List badges earned by a user | Yes | Authenticated |
| `GET` | `/api/favorites` | List caller's bookmarked favorite users | Yes | Authenticated |
| `POST` | `/api/favorites/{userId}` | Add user to favorites list | Yes | Authenticated |
| `DELETE` | `/api/favorites/{userId}` | Remove user from favorites list | Yes | Authenticated |
| `POST` | `/api/blocks/{userId}` | Block another user | Yes | Authenticated |
| `DELETE` | `/api/blocks/{userId}` | Unblock a user | Yes | Authenticated |
| `POST` | `/api/reports` | Submit a moderation report against a user | Yes | Authenticated |
| `GET` | `/api/subscriptions/plans` | List available subscription plans | No | Anonymous |
| `GET` | `/api/subscriptions/me` | Get caller's active subscription plan | Yes | Authenticated |
| `POST` | `/api/subscriptions/subscribe` | Mock upgrade to a subscription plan | Yes | Authenticated |

---

## 13. Existing Implemented Endpoints
*None currently in API controllers.*

---

## 14. Events / SignalR / Notifications
- Triggers creation of `Notification` records on incoming SwapRequests, Session reminders, cancellations, and reviews.
- Allows real-time delivery of notifications via SignalR if connected.

---

## 15. Database Responsibility
- Tables: `Reviews`, `Notifications`, `Badges`, `UserBadges`, `Favorites`, `UserBlocks`, `Reports`, `SubscriptionPlans`, `UserSubscriptions`.
- Composite PKs:
  - `Favorites`: `(UserId, FavoriteUserId)`
  - `UserBlocks`: `(BlockerId, BlockedId)`
  - `UserBadges`: `(UserId, BadgeId)`
- Coordinate migrations with Member 4.

---

## 16. Validation & Authorization
- `CreateReviewRequest`: `Rating` between 1 and 5, `Comment` max 1000 chars.
- `CreateReportRequest`: Valid `ReportReason` enum, `Description` max 2000 chars.
- All mutating endpoints enforce `[Authorize]`.

---

## 17. Testing Requirements
- **Unit Tests:**
  - Submitting review for non-completed session throws `InvalidOperationException`.
  - Submitting review with rating 0 or 6 fails validation.
  - Duplicate review from same user for same session is rejected.
  - Blocking prevents swap creation.
- **Integration Tests:**
  - Mock subscription upgrade updates active status and enables 720-minute cap.

---

## 18. Definition of Done
- [ ] Review submission enforces completed session rule and rating bounds.
- [ ] Notifications center implemented with unread filter and mark-read endpoint.
- [ ] Block and favorite logic fully operational and covered by unit tests.
- [ ] Premium plan price verified as `NULL` without payment gateway dependencies.
- [ ] 100% test pass rate on `dotnet test`.
- [ ] PR reviewed and approved by Member 4 and one other member.

---

## 19. PR Checklist
- [ ] Does `dotnet test` pass with 0 errors?
- [ ] Are rating bounds (1-5) strictly enforced?
- [ ] Is payment gateway integration strictly avoided?
- [ ] Is Premium pricing preserved as unresolved (`NULL`)?
- [ ] Does the branch follow `feature/reviews-platform`?

---

## 20. Common Mistakes to Avoid
- **Mistake 1:** Introducing Stripe or PayPal payment SDKs. (Postponed from MVP).
- **Mistake 2:** Hardcoding `SubscriptionPlan.Price = 9.99`. (Must remain `NULL` for Premium).
- **Mistake 3:** Allowing reviews on sessions with status `Scheduled` or `Disputed`. (Only `Completed` sessions can be reviewed).
- **Mistake 4:** Modifying `Wallet` balances or credit ledgers directly from platform services.

---
*Member 5 Ownership Guide -- SkillSwap Backend Team*
