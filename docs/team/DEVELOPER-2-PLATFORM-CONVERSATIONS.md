# Developer 2 — Platform Features & Conversations REST API

**Developer:** [Arwa Khalid](https://github.com/ArwaKhalidd) (@ArwaKhalidd)  
**Role:** Developer 2  
**Module:** Platform Features (Reviews, Notifications, Badges, Favorites, Blocks, Reports, Subscriptions) & Conversations REST API  

---

## 1. Mission

Deliver high-quality user engagement, trust, social safety, and community interaction systems for SkillSwap, and maintain the public REST API surface for conversations and message exchanges. Provide comprehensive post-session feedback, asynchronous user alerting, gamification badges, user safety controls (blocks and moderation reports), tier capability declarations, and the full REST controller implementation for conversation history, read receipts, and REST message dispatch.

---

## 2. Scope of Ownership

### Platform Features
- **Post-Session Reviews:** Ratings (1 to 5 stars) and feedback comments on completed sessions (`Review`).
- **Notification Center:** In-app user notifications, event tracking, and read receipts (`Notification`).
- **Gamification & Badges:** Master achievement catalog and user badge awards (`Badge`, `UserBadge`).
- **Social & Safety Controls:** User bookmarks (`Favorite`), communication blocks (`UserBlock`), and moderation flags (`Report`).
- **Subscriptions & Quota Tiers:** Subscription plan catalog (`SubscriptionPlan`) and user enrollments (`UserSubscription`) defining monthly learning limits (180 min Free vs 720 min Premium).
- **Monetization Invariant:** **Payment gateways (Stripe, PayPal) are strictly out of MVP scope.** Premium tier pricing is not finalized (`SubscriptionPlan.Price = NULL`).

### Conversations REST API (Redistributed Responsibilities)
- **ConversationsController REST Endpoints:** Complete HTTP API for conversations and messaging.
- **Listing Conversations:** `GET /api/conversations` for the authenticated caller.
- **Conversation Details:** `GET /api/conversations/{id:long}` with participant membership validation.
- **Message Retrieval:** `GET /api/conversations/{id:long}/messages` with pagination (`page`, `pageSize`).
- **REST Message Dispatch:** `POST /api/conversations/{id:long}/messages` to persist messages and trigger SignalR group notification fallback.
- **Read Cursor Updates:** `POST /api/conversations/{id:long}/read` updating `LastReadMessageId`.
- **Chat DTOs & Validation:** `ConversationDto`, `MessageDto`, `SendMessageRequest`, `MarkConversationReadRequest`.
- **REST Controller Security:** Enforcing JWT authentication (`[Authorize]`), 401 Unauthorized, 403 Forbidden for non-participants, 404 NotFound, and 400 BadRequest validation errors.
- **REST Controller Tests:** Unit tests in `ConversationsControllerTests.cs`.

---

## 3. Domain Entities Owned

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

## 4. Application Services Owned

| Interface / Class | Location | Status | Description |
|---|---|---|---|
| `IReviewService` | `src/SkillSwap.Application/Abstractions/IReviewService.cs` | **IMPLEMENTED** | Manages session reviews, rating validation, and public feedback. |
| `ReviewService` | `src/SkillSwap.Application/Services/ReviewService.cs` | **IMPLEMENTED** | Implements review submission and aggregation. |
| `INotificationService` | `src/SkillSwap.Application/Abstractions/INotificationService.cs` | **IMPLEMENTED** | Dispatches, queries, and marks user notifications. |
| `NotificationService` | `src/SkillSwap.Application/Services/NotificationService.cs` | **IMPLEMENTED** | Notification management and read tracking. |
| `IBadgeService` | `src/SkillSwap.Application/Abstractions/IBadgeService.cs` | **IMPLEMENTED** | Queries badges and awards achievements. |
| `BadgeService` | `src/SkillSwap.Application/Services/BadgeService.cs` | **IMPLEMENTED** | Badge catalog and user badge retrieval. |
| `IFavoriteService` | `src/SkillSwap.Application/Abstractions/IFavoriteService.cs` | **IMPLEMENTED** | Manages user favorite bookmarks. |
| `FavoriteService` | `src/SkillSwap.Application/Services/FavoriteService.cs` | **IMPLEMENTED** | Add, remove, and list favorite users. |
| `IBlockService` | `src/SkillSwap.Application/Abstractions/IBlockService.cs` | **IMPLEMENTED** | Manages user blocks and validates communication permissions. |
| `BlockService` | `src/SkillSwap.Application/Services/BlockService.cs` | **IMPLEMENTED** | User block and unblock handling. |
| `IReportService` | `src/SkillSwap.Application/Abstractions/IReportService.cs` | **IMPLEMENTED** | Submits and tracks user moderation reports. |
| `ReportService` | `src/SkillSwap.Application/Services/ReportService.cs` | **IMPLEMENTED** | Moderation reporting and review workflows. |
| `ISubscriptionService` | `src/SkillSwap.Application/Abstractions/ISubscriptionService.cs` | **IMPLEMENTED** | Queries plans and manages active user subscription status. |
| `SubscriptionService` | `src/SkillSwap.Application/Services/SubscriptionService.cs` | **IMPLEMENTED** | Tier catalog and mock subscription management. |

---

## 5. Controllers & Endpoints Owned

| Controller | Route Area | Method & Endpoint | Description |
|---|---|---|---|
| `ConversationsController` | `/api/conversations` | `GET /api/conversations` | List all conversations for the authenticated user. |
| `ConversationsController` | `/api/conversations` | `GET /api/conversations/{id:long}` | Retrieve specific conversation metadata. |
| `ConversationsController` | `/api/conversations` | `GET /api/conversations/{id:long}/messages` | Paginated message history for a conversation. |
| `ConversationsController` | `/api/conversations` | `POST /api/conversations/{id:long}/messages` | Send a new message via REST API (broadcasts to SignalR). |
| `ConversationsController` | `/api/conversations` | `POST /api/conversations/{id:long}/read` | Update user read cursor (`LastReadMessageId`). |
| `ReviewsController` | `/api/reviews` | `POST /api/reviews` | Submit a review for a completed session. |
| `ReviewsController` | `/api/reviews` | `GET /api/reviews/user/{userId}` | List reviews received by a user. |
| `NotificationsController` | `/api/notifications` | `GET /api/notifications` | List notifications for the authenticated user. |
| `NotificationsController` | `/api/notifications` | `POST /api/notifications/{id}/read` | Mark a notification as read. |
| `BadgesController` | `/api/badges` | `GET /api/badges` | List all catalog badges. |
| `BadgesController` | `/api/badges` | `GET /api/badges/user/{userId}` | List badges awarded to a user. |
| `FavoritesController` | `/api/favorites` | `GET /api/favorites` | List favorite users for the authenticated caller. |
| `FavoritesController` | `/api/favorites` | `POST /api/favorites/{userId}` | Add a user to favorites. |
| `FavoritesController` | `/api/favorites` | `DELETE /api/favorites/{userId}` | Remove a user from favorites. |
| `BlocksController` | `/api/blocks` | `POST /api/blocks/{userId}` | Block another user from interactions. |
| `BlocksController` | `/api/blocks` | `DELETE /api/blocks/{userId}` | Unblock a user. |
| `ReportsController` | `/api/reports` | `POST /api/reports` | Submit a moderation report. |
| `SubscriptionsController` | `/api/subscriptions` | `GET /api/subscriptions/plans` | List available subscription plans. |
| `SubscriptionsController` | `/api/subscriptions` | `GET /api/subscriptions/me` | Retrieve current user's active plan. |
| `SubscriptionsController` | `/api/subscriptions` | `POST /api/subscriptions/subscribe` | Mock subscribe to a plan (No payment gateway). |

---

## 6. Files Owned

### Entities & Configurations
- `src/SkillSwap.Domain/Entities/Review.cs`
- `src/SkillSwap.Domain/Entities/Notification.cs`
- `src/SkillSwap.Domain/Entities/Badge.cs`
- `src/SkillSwap.Domain/Entities/UserBadge.cs`
- `src/SkillSwap.Domain/Entities/Favorite.cs`
- `src/SkillSwap.Domain/Entities/UserBlock.cs`
- `src/SkillSwap.Domain/Entities/Report.cs`
- `src/SkillSwap.Domain/Entities/SubscriptionPlan.cs`
- `src/SkillSwap.Domain/Entities/UserSubscription.cs`
- Configurations in `src/SkillSwap.Infrastructure/Persistence/Configurations/` for the above entities.

### DTOs
- `src/SkillSwap.Application/DTOs/Chat/ConversationDto.cs`
- `src/SkillSwap.Application/DTOs/Chat/MessageDto.cs`
- `src/SkillSwap.Application/DTOs/Chat/SendMessageRequest.cs`
- `src/SkillSwap.Application/DTOs/Chat/MarkConversationReadRequest.cs`
- `src/SkillSwap.Application/DTOs/Reviews/*`
- `src/SkillSwap.Application/DTOs/Notifications/*`
- `src/SkillSwap.Application/DTOs/Badges/*`
- `src/SkillSwap.Application/DTOs/Favorites/*`
- `src/SkillSwap.Application/DTOs/Blocks/*`
- `src/SkillSwap.Application/DTOs/Reports/*`
- `src/SkillSwap.Application/DTOs/Subscriptions/*`

### Services & Controllers
- `src/SkillSwap.API/Controllers/ConversationsController.cs`
- `src/SkillSwap.API/Controllers/ReviewsController.cs`
- `src/SkillSwap.API/Controllers/NotificationsController.cs`
- `src/SkillSwap.API/Controllers/BadgesController.cs`
- `src/SkillSwap.API/Controllers/FavoritesController.cs`
- `src/SkillSwap.API/Controllers/BlocksController.cs`
- `src/SkillSwap.API/Controllers/ReportsController.cs`
- `src/SkillSwap.API/Controllers/SubscriptionsController.cs`
- Matching service classes under `src/SkillSwap.Application/Services/`

### Test Suites
- `tests/SkillSwap.Tests/Unit/ConversationsControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/ReviewsControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/ReviewServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/NotificationsControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/NotificationServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/BadgesControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/BadgeServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/FavoritesControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/FavoriteServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/BlocksControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/BlockServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/ReportsControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/ReportServiceTests.cs`
- `tests/SkillSwap.Tests/Unit/SubscriptionsControllerTests.cs`
- `tests/SkillSwap.Tests/Unit/SubscriptionServiceTests.cs`

---

## 7. Cross-Module Contracts & Dependencies

1. **Session Completion Dependency for Reviews:**
   - A user can only submit a review (`POST /api/reviews`) for a session that has reached `SessionStatus.Completed`.
2. **Quota Invariant with Developer 1:**
   - Developer 1's `QuotaService` queries `UserSubscription` to verify whether the learner's monthly learning cap is 180 minutes (Free) or 720 minutes (Premium).
3. **Safety Blocks Invariant with Developers 1 & 3:**
   - Active blocks in `UserBlocks` suppress matching recommendations (Developer 3), reject new swap requests (Developer 1), and deny conversation access.
4. **Coordination with Developer 1:**
   - `ConversationsController.SendMessage` delegates persistence to `IChatService` and coordinates SignalR broadcasting with Developer 1's `ChatHub`.
   - All schema modifications are submitted to Developer 1 for EF Core migrations.

---
*Developer 2 Ownership Guide — SkillSwap Backend Engineering Team*
