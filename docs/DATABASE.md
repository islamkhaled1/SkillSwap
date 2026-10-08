# DATABASE.md - SkillSwap Database Specification

> **SOURCE OF TRUTH** for all database design decisions.
>
> All agents and developers MUST read this document before creating or modifying entities.
> Do NOT add fields, change relationships, or alter constraints without updating this document first (PR required).

---

## Design Principles

- Primary Keys use integer/bigint identity types for relational performance (`int` for reference tables, `bigint` for transactional tables) and `Guid` for Identity users and 1:1 user-anchored models (`UserProfile`, `Wallet`).
- All timestamps use UTC and are named with the `*Utc` suffix (`datetime2`).
- Recurring availability slots use SQL Server `time` (`TimeOnly`).
- Soft deletes on user accounts and financial records; hard deletes never on financial data.
- `DeleteBehavior.Restrict` or `DeleteBehavior.NoAction` on all financial/session foreign keys to prevent accidental cascades and avoid SQL Server multiple cascade path errors.
- EF Core `IEntityTypeConfiguration<T>` pattern for all entity mappings located in `src/SkillSwap.Infrastructure/Persistence/Configurations/`.
- Session overlap prevention is enforced via **transactional application logic + wallet UPDLOCK**, not database range constraints (SQL Server does not support exclusion constraints like PostgreSQL).

---

## Entities

---

### ApplicationUser

> Extends `IdentityUser<Guid>`. Core identity record (mapped to table `Users`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | Guid | PK | IdentityUser default |
| `UserName` | nvarchar(256) | Unique | Set to email |
| `NormalizedUserName` | nvarchar(256) | Unique | |
| `Email` | nvarchar(256) | Unique, NOT NULL | |
| `NormalizedEmail` | nvarchar(256) | Unique | |
| `PasswordHash` | nvarchar(max) | | |
| `CreatedAtUtc` | datetime2 | NOT NULL, DEFAULT UTC | |
| `UpdatedAtUtc` | datetime2 | NULL | |
| `IsDeleted` | bit | NOT NULL, DEFAULT 0 | Soft delete flag |
| `DeletedAtUtc` | datetime2 | NULL | |

---

### UserProfile

> Extended profile data, separated from identity to keep ApplicationUser lean (mapped to `UserProfiles`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `UserId` | Guid | PK, FK -> Users | 1:1, DeleteBehavior.Cascade |
| `DisplayName` | nvarchar(100) | NOT NULL | |
| `Bio` | nvarchar(1000) | NULL | |
| `PhotoUrl` | nvarchar(500) | NULL | Avatar/photo URL |
| `TimeZoneId` | nvarchar(100) | NOT NULL | Default "UTC" |
| `IsVerified` | bit | NOT NULL, DEFAULT 0 | Verification status |
| `CreatedAtUtc` | datetime2 | NOT NULL | |
| `UpdatedAtUtc` | datetime2 | NULL | |

*Rule:* Ratings and counts are calculated dynamically from `Reviews` (not stored as source of truth).

---

### Category

> Top-level skill categories (mapped to `Categories`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | int | PK, Identity | |
| `Name` | nvarchar(100) | Unique, NOT NULL | |
| `IsActive` | bit | NOT NULL, DEFAULT 1 | |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

---

### Skill

> Specific skills within categories (mapped to `Skills`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | int | PK, Identity | |
| `CategoryId` | int | FK -> Categories, NOT NULL | DeleteBehavior.Restrict |
| `Name` | nvarchar(100) | NOT NULL | Unique per (CategoryId, Name) |
| `IsActive` | bit | NOT NULL, DEFAULT 1 | |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

**Indexes:**
- `IX_Skills_CategoryId`
- `IX_Skills_CategoryId_Name` (Unique)

---

### UserSkill

> Associative table for skills taught or learned by a user (mapped to `UserSkills`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `UserId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Cascade |
| `SkillId` | int | FK -> Skills, NOT NULL | DeleteBehavior.Restrict |
| `Type` | tinyint | NOT NULL | Enum: Teach=1, Learn=2 |
| `Level` | tinyint | NOT NULL | Enum: Beginner=1, Intermediate=2, Advanced=3 |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

**Indexes:**
- Unique Index: `(UserId, SkillId, Type)`
- Lookup Index: `IX_UserSkill_SkillId_Type_UserId` on `(SkillId, Type, UserId)`

---

### AvailabilitySlot

> Time windows when a user is available to teach (mapped to `AvailabilitySlots`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `UserId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Cascade |
| `DayOfWeek` | tinyint | NOT NULL | 0=Sunday..6=Saturday |
| `StartTime` | time | NOT NULL | |
| `EndTime` | time | NOT NULL | |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

**Check Constraints:**
- `CK_AvailabilitySlot_StartTime_EndTime`: `[StartTime] < [EndTime]`

**Indexes:**
- `IX_AvailabilitySlots_UserId_DayOfWeek`

---

### SwapRequest

> Request to exchange/learn a skill (mapped to `SwapRequests`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `RequesterId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Restrict |
| `ReceiverId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Restrict |
| `SkillId` | int | FK -> Skills, NOT NULL | DeleteBehavior.Restrict |
| `Message` | nvarchar(1000) | NULL | |
| `Status` | tinyint | NOT NULL | Pending=1, Accepted=2, Declined=3, Cancelled=4, Expired=5 |
| `CreatedAtUtc` | datetime2 | NOT NULL | |
| `RespondedAtUtc` | datetime2 | NULL | |
| `ExpiresAtUtc` | datetime2 | NULL | ~7 days expiration |
| `CancelledAtUtc` | datetime2 | NULL | |

**Check Constraints:**
- `CK_SwapRequest_Requester_Receiver`: `[RequesterId] <> [ReceiverId]`

**Indexes:**
- Filtered Unique Index: `UX_SwapRequest_ActivePending` on `(RequesterId, ReceiverId, SkillId)` WHERE `[Status] = 1`
- `IX_SwapRequests_RequesterId_Status`
- `IX_SwapRequests_ReceiverId_Status`

---

### Conversation

> Chat room created upon swap request acceptance (mapped to `Conversations`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `SwapRequestId` | bigint | FK -> SwapRequests, Unique, NOT NULL | DeleteBehavior.Cascade |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

**Indexes:**
- `UX_Conversation_SwapRequestId` (Unique)

---

### ConversationParticipant

> Chat room participants (mapped to `ConversationParticipants`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `ConversationId` | bigint | PK, FK -> Conversations | DeleteBehavior.Cascade |
| `UserId` | Guid | PK, FK -> Users | DeleteBehavior.Restrict |
| `LastReadMessageId` | bigint | NULL | |
| `JoinedAtUtc` | datetime2 | NOT NULL | |

**Primary Key:** `(ConversationId, UserId)`

---

### Message

> Individual chat message (mapped to `Messages`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `ConversationId` | bigint | FK -> Conversations, NOT NULL | DeleteBehavior.Cascade |
| `SenderId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Restrict |
| `Body` | nvarchar(2000) | NOT NULL | |
| `SentAtUtc` | datetime2 | NOT NULL | |
| `IsDeleted` | bit | NOT NULL, DEFAULT 0 | |

**Indexes:**
- `IX_Message_ConversationId_Id` on `(ConversationId, Id)`

---

### Session

> Scheduled session between teacher and learner (mapped to `Sessions`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `SwapRequestId` | bigint | FK -> SwapRequests, NOT NULL | DeleteBehavior.Restrict |
| `TeacherId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Restrict |
| `LearnerId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Restrict |
| `SkillId` | int | FK -> Skills, NOT NULL | Historical snapshot; DeleteBehavior.Restrict |
| `StartUtc` | datetime2 | NOT NULL | |
| `EndUtc` | datetime2 | NOT NULL | StartUtc + DurationMinutes |
| `DurationMinutes` | smallint | NOT NULL | Allowed: 30, 60, 90 |
| `Mode` | tinyint | NOT NULL | Online=1, InPerson=2 |
| `MeetingUrl` | nvarchar(1000) | NULL | |
| `Status` | tinyint | NOT NULL | PendingConfirmation=1, Scheduled=2, Completed=3, Cancelled=4, NoShow=5, Disputed=6, Expired=7 |
| `CancelledById` | Guid | FK -> Users, NULL | DeleteBehavior.Restrict |
| `CancelledAtUtc` | datetime2 | NULL | |
| `NoShowUserId` | Guid | FK -> Users, NULL | DeleteBehavior.Restrict |
| `LearnerJoinedAtUtc` | datetime2 | NULL | |
| `TeacherJoinedAtUtc` | datetime2 | NULL | |
| `ReportedById` | Guid | FK -> Users, NULL | Dispute metadata; DeleteBehavior.Restrict |
| `ResolvedAtUtc` | datetime2 | NULL | Dispute metadata |
| `ResolutionNote` | nvarchar(1000) | NULL | Dispute metadata |
| `CreatedAtUtc` | datetime2 | NOT NULL | |
| `UpdatedAtUtc` | datetime2 | NULL | |
| `RowVersion` | rowversion | NOT NULL | Concurrency token |

**Check Constraints:**
- `CK_Session_Teacher_Learner`: `[TeacherId] <> [LearnerId]`
- `CK_Session_DurationMinutes`: `[DurationMinutes] IN (30, 60, 90)`

**Indexes:**
- `IX_Session_TeacherId_StartUtc` on `(TeacherId, StartUtc)`
- `IX_Session_LearnerId_StartUtc` on `(LearnerId, StartUtc)`
- `IX_Session_Status_EndUtc` on `(Status, EndUtc)`

---

### Wallet

> User time credit balance (mapped to `Wallets`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `UserId` | Guid | PK, FK -> Users | 1:1, DeleteBehavior.Restrict |
| `AvailableMinutes` | int | NOT NULL, DEFAULT 0, CHECK >= 0 | Spendable balance |
| `HeldMinutes` | int | NOT NULL, DEFAULT 0, CHECK >= 0 | Reserved for scheduled sessions |
| `UpdatedAtUtc` | datetime2 | NOT NULL | |
| `RowVersion` | rowversion | NOT NULL | Concurrency token |

**Check Constraints:**
- `CK_Wallet_AvailableMinutes`: `[AvailableMinutes] >= 0`
- `CK_Wallet_HeldMinutes`: `[HeldMinutes] >= 0`

---

### CreditTransaction

> Append-only transaction ledger (mapped to `CreditTransactions`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `WalletId` | Guid | FK -> Wallets, NOT NULL | DeleteBehavior.Restrict |
| `SessionId` | bigint | FK -> Sessions, NULL | DeleteBehavior.Restrict |
| `Type` | tinyint | NOT NULL | Bonus=1, Hold=2, Release=3, Capture=4, Earn=5, Adjustment=6 |
| `AvailableDelta` | int | NOT NULL | Net change to AvailableMinutes |
| `HeldDelta` | int | NOT NULL | Net change to HeldMinutes |
| `CreatedAtUtc` | datetime2 | NOT NULL | |
| `Description` | nvarchar(500) | NULL | |

**Indexes:**
- Filtered Unique Index: `UX_CreditTransaction_Wallet_Session_Type` on `(WalletId, SessionId, Type)` WHERE `[SessionId] IS NOT NULL AND [Type] IN (2, 3, 4, 5)`
- `IX_CreditTransaction_WalletId_CreatedAtUtc`
- `IX_CreditTransaction_SessionId`

---

### Review

> Feedback left after a completed session (mapped to `Reviews`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `SessionId` | bigint | FK -> Sessions, NOT NULL | DeleteBehavior.Restrict |
| `ReviewerId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Restrict |
| `RevieweeId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Restrict |
| `Rating` | tinyint | NOT NULL, CHECK 1..5 | 1 to 5 stars |
| `Comment` | nvarchar(1000) | NULL | |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

**Check Constraints:**
- `CK_Review_Rating`: `[Rating] >= 1 AND [Rating] <= 5`
- `CK_Review_Reviewer_Reviewee`: `[ReviewerId] <> [RevieweeId]`

**Indexes:**
- `UX_Review_SessionId_ReviewerId` (Unique) on `(SessionId, ReviewerId)`

---

### SubscriptionPlan

> Reference subscription plans (mapped to `SubscriptionPlans`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | int | PK, Identity | |
| `Code` | varchar(30) | Unique, NOT NULL | "FREE", "PREMIUM" |
| `Name` | nvarchar(100) | NOT NULL | "Free", "Premium" |
| `MonthlyLearningMinutes` | int | NOT NULL | 180 (Free), 720 (Premium) |
| `Price` | decimal(10,2) | NULL | 0.00 for Free, NULL for Premium (pricing not finalized in MVP) |
| `PriorityMatching` | bit | NOT NULL | |
| `AdvancedSearch` | bit | NOT NULL | |
| `IsActive` | bit | NOT NULL, DEFAULT 1 | |

**Seed Data:**
- `Id = 1, Code = "FREE", Name = "Free", MonthlyLearningMinutes = 180, Price = 0.00, PriorityMatching = false, AdvancedSearch = false, IsActive = true`
- `Id = 2, Code = "PREMIUM", Name = "Premium", MonthlyLearningMinutes = 720, Price = NULL, PriorityMatching = true, AdvancedSearch = true, IsActive = true`

---

### UserSubscription

> Active and past user subscriptions (mapped to `UserSubscriptions`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `UserId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Cascade |
| `PlanId` | int | FK -> SubscriptionPlans, NOT NULL | DeleteBehavior.Restrict |
| `StartsAtUtc` | datetime2 | NOT NULL | |
| `EndsAtUtc` | datetime2 | NULL | NULL = active / open-ended |
| `Status` | tinyint | NOT NULL | Active=1, Cancelled=2, Expired=3 |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

**Indexes:**
- Filtered Unique Index: `UX_UserSubscription_ActiveUser` on `(UserId)` WHERE `[Status] = 1`

---

### Notification

> In-app user notifications (mapped to `Notifications`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `UserId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Cascade |
| `Type` | tinyint | NOT NULL | Enum: SwapRequest=1..System=8 |
| `Payload` | nvarchar(max) | NOT NULL | JSON payload |
| `IsRead` | bit | NOT NULL, DEFAULT 0 | |
| `ReadAtUtc` | datetime2 | NULL | |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

**Indexes:**
- `IX_Notification_UserId_IsRead_CreatedAtUtc` on `(UserId, IsRead, CreatedAtUtc DESC)`

---

### Badge

> Achievement definitions (mapped to `Badges`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | int | PK, Identity | |
| `Code` | varchar(50) | Unique, NOT NULL | |
| `Name` | nvarchar(100) | NOT NULL | |
| `Description` | nvarchar(500) | NOT NULL | |
| `IconUrl` | nvarchar(500) | NULL | |

---

### UserBadge

> Badges earned by users (mapped to `UserBadges`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `UserId` | Guid | PK, FK -> Users | DeleteBehavior.Cascade |
| `BadgeId` | int | PK, FK -> Badges | DeleteBehavior.Restrict |
| `AwardedAtUtc` | datetime2 | NOT NULL | |

**Primary Key:** `(UserId, BadgeId)`

---

### Favorite

> User bookmarking favorite user (mapped to `Favorites`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `UserId` | Guid | PK, FK -> Users | DeleteBehavior.Cascade |
| `FavoriteUserId` | Guid | PK, FK -> Users | DeleteBehavior.Restrict |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

**Primary Key:** `(UserId, FavoriteUserId)`
**Check Constraints:**
- `CK_Favorite_User_NotSelf`: `[UserId] <> [FavoriteUserId]`

---

### UserBlock

> User blocking another user (mapped to `UserBlocks`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `BlockerId` | Guid | PK, FK -> Users | DeleteBehavior.Cascade |
| `BlockedId` | Guid | PK, FK -> Users | DeleteBehavior.Restrict |
| `CreatedAtUtc` | datetime2 | NOT NULL | |

**Primary Key:** `(BlockerId, BlockedId)`
**Check Constraints:**
- `CK_UserBlock_Blocker_NotSelf`: `[BlockerId] <> [BlockedId]`

---

### Report

> Moderation incident reports (mapped to `Reports`).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | bigint | PK, Identity | |
| `ReporterId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Restrict |
| `ReportedUserId` | Guid | FK -> Users, NOT NULL | DeleteBehavior.Restrict |
| `SessionId` | bigint | FK -> Sessions, NULL | DeleteBehavior.Restrict |
| `Reason` | tinyint | NOT NULL | Harassment=1, InappropriateContent=2, Spam=3, NoShow=4, Fraud=5, Other=6 |
| `Description` | nvarchar(1000) | NULL | |
| `Status` | tinyint | NOT NULL | Open=1, UnderReview=2, Resolved=3, Rejected=4 |
| `CreatedAtUtc` | datetime2 | NOT NULL | |
| `ResolvedAtUtc` | datetime2 | NULL | |

**Check Constraints:**
- `CK_Report_Reporter_NotReported`: `[ReporterId] <> [ReportedUserId]`

**Indexes:**
- `IX_Report_ReportedUserId_Status` on `(ReportedUserId, Status)`

---

## Indexes Summary

| Table | Index | Columns | Type / Filter | Notes |
|---|---|---|---|---|
| Session | IX_Session_TeacherId_StartUtc | (TeacherId, StartUtc) | Non-unique | Availability queries |
| Session | IX_Session_LearnerId_StartUtc | (LearnerId, StartUtc) | Non-unique | Learner history |
| Session | IX_Session_Status_EndUtc | (Status, EndUtc) | Non-unique | Background expiration/completion jobs |
| UserSkill | IX_UserSkill_SkillId_Type_UserId | (SkillId, Type, UserId) | Non-unique | Matching algorithms |
| UserSkill | IX_UserSkills_UserId_SkillId_Type | (UserId, SkillId, Type) | Unique | Prevent duplicate user skills |
| Notification | IX_Notification_UserId_IsRead_CreatedAtUtc | (UserId, IsRead, CreatedAtUtc DESC) | Non-unique | Unread notification list |
| UserSubscription | UX_UserSubscription_ActiveUser | (UserId) | Unique, WHERE `[Status] = 1` | Single active subscription invariant |
| SwapRequest | UX_SwapRequest_ActivePending | (RequesterId, ReceiverId, SkillId) | Unique, WHERE `[Status] = 1` | No duplicate active pending requests |
| Conversation | UX_Conversation_SwapRequestId | (SwapRequestId) | Unique | 1:1 SwapRequest-Conversation relationship |
| Review | UX_Review_SessionId_ReviewerId | (SessionId, ReviewerId) | Unique | Single review per reviewer per session |
| CreditTransaction | UX_CreditTransaction_Wallet_Session_Type | (WalletId, SessionId, Type) | Unique, WHERE `[SessionId] IS NOT NULL AND [Type] IN (2, 3, 4, 5)` | Idempotent session credit transfers |

---

## Concurrency and Integrity Architecture

1. **Deterministic Lock Ordering:**
   Booking a session requires transactional concurrency protection:
   - Start SQL Server transaction
   - Lock both learner and teacher Wallet rows in deterministic order: `MIN(LearnerId, TeacherId)` followed by `MAX(LearnerId, TeacherId)` using `UPDLOCK`
   - Validate balance (`AvailableMinutes >= duration`)
   - Check overlapping sessions for teacher and learner
   - Hold minutes atomically: Learner `AvailableMinutes -= duration`, `HeldMinutes += duration`
   - Insert append-only `CreditTransaction` (Type = `Hold`, AvailableDelta = -duration, HeldDelta = +duration)

2. **Cascade Path Protection:**
   SQL Server prohibits multiple cascade delete paths to avoid cycles. All multi-relationship user references (`TeacherId`, `LearnerId`, `ReviewerId`, `RevieweeId`, `BlockerId/BlockedId`, `ReporterId/ReportedUserId`, `CancelledById`, `NoShowUserId`, `ReportedById`) are explicitly configured with `DeleteBehavior.Restrict` or `DeleteBehavior.NoAction`.

