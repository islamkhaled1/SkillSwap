# Member 3 -- Swap Request & Chat

## 1. Mission
Own the negotiation phase of the skill exchange lifecycle, including SwapRequest creation, acceptance, and decline workflows, 1-on-1 Conversation management, message persistence, and real-time messaging via SignalR (`ChatHub`). Ensure the ironclad Requester/Receiver role contract is enforced across all pre-session interactions.

---

## 2. Scope of Ownership
- SwapRequest negotiation lifecycle (`Pending`, `Accepted`, `Declined`, `Cancelled`, `Expired`).
- Conversation provisioning upon SwapRequest acceptance.
- 1-on-1 messaging history, unread counters, and message pagination.
- Real-time SignalR `ChatHub` implementation (`/hubs/chat`).
- Notification triggers for swap proposals and new messages.
- Non-completed contract: SwapRequest **never** has a `Completed` status.

---

## 3. Responsibilities
- Implement SwapRequest REST APIs (`POST /api/swap-requests`, incoming/outgoing listings, accept/decline/cancel).
- Ensure accepting a SwapRequest automatically creates a `Conversation` record with both users as `ConversationParticipant`.
- Implement conversation history queries (`GET /api/conversations/{id}/messages`) with cursor or page-based pagination.
- Complete business implementation of `ChatHub` methods (`SendMessage`, `JoinConversation`, `LeaveConversation`).
- Guarantee that `SwapRequest.RequesterId` is always the Learner and `SwapRequest.ReceiverId` is always the Teacher.
- Write unit tests verifying negotiation state machines and conversation authorization security.

---

## 4. Domain Entities Owned

| Entity | Primary Key | Table | Description |
|---|---|---|---|
| `SwapRequest` | `long` | `SwapRequests` | Proposal from learner to teacher to exchange skills. |
| `Conversation` | `long` | `Conversations` | 1-on-1 message container linked 1:1 with an accepted SwapRequest. |
| `ConversationParticipant` | `(long, Guid)` | `ConversationParticipants` | Tracks user membership in conversation and `LastReadMessageId`. |
| `Message` | `long` | `Messages` | Individual chat message with body, sender, and timestamp. |

### Enums Owned:
- `SwapRequestStatus` (`Pending = 1`, `Accepted = 2`, `Declined = 3`, `Cancelled = 4`, `Expired = 5`).
  *(Note: SwapRequestStatus intentionally does NOT contain Completed).*

---

## 5. Application Services Owned

| Interface / Class | Location | Status | Description |
|---|---|---|---|
| `ISwapRequestService` | `src/SkillSwap.Application/Abstractions/ISwapRequestService.cs` | **PLANNED** | Manages swap proposals, status transitions, and expiration. |
| `IChatService` | `src/SkillSwap.Application/Abstractions/IChatService.cs` | **PLANNED** | Handles conversation listing, message history, and unread flags. |

---

## 6. Controllers & Hubs Owned

| Component | Route / Hub Path | Status | Description |
|---|---|---|---|
| `SwapRequestsController` | `/api/swap-requests` | **PLANNED** | REST APIs for proposing, accepting, declining, and listing swap requests. |
| `ConversationsController` | `/api/conversations` | **PLANNED** | REST APIs for retrieving conversations and message history. |
| `ChatHub` | `/hubs/chat` | **IMPLEMENTED (Foundation)** | SignalR hub handling real-time WebSocket messaging. |

---

## 7. Files I Own

### Entities & Configurations:
- `src/SkillSwap.Domain/Entities/SwapRequest.cs`
- `src/SkillSwap.Domain/Entities/Conversation.cs`
- `src/SkillSwap.Domain/Entities/ConversationParticipant.cs`
- `src/SkillSwap.Domain/Entities/Message.cs`
- `src/SkillSwap.Domain/Enums/SwapRequestStatus.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/SwapRequestConfiguration.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/ConversationConfiguration.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/ConversationParticipantConfiguration.cs`
- `src/SkillSwap.Infrastructure/Persistence/Configurations/MessageConfiguration.cs`

### API & Hubs:
- `src/SkillSwap.API/Hubs/ChatHub.cs`
- Planned: `src/SkillSwap.API/Controllers/SwapRequestsController.cs`
- Planned: `src/SkillSwap.API/Controllers/ConversationsController.cs`

### Planned Application DTOs & Services:
- `src/SkillSwap.Application/DTOs/SwapRequests/CreateSwapRequest.cs`
- `src/SkillSwap.Application/DTOs/SwapRequests/SwapRequestDto.cs`
- `src/SkillSwap.Application/DTOs/Chat/ConversationDto.cs`
- `src/SkillSwap.Application/DTOs/Chat/MessageDto.cs`
- `src/SkillSwap.Application/DTOs/Chat/SendMessageRequest.cs`
- `src/SkillSwap.Application/Services/SwapRequestService.cs`
- `src/SkillSwap.Application/Services/ChatService.cs`

---

## 8. Files I Must Not Modify Without Coordination

- `src/SkillSwap.Domain/Entities/Session.cs` (Owner: Member 4)
- `src/SkillSwap.Domain/Entities/Wallet.cs` (Owner: Member 4)
- `src/SkillSwap.Application/Services/SessionBookingService.cs` (Owner: Member 4)
- `src/SkillSwap.Infrastructure/Persistence/ApplicationDbContext.cs` (Owner: Member 4)
- `src/SkillSwap.Infrastructure/Migrations/*` (Owner: Member 4 ONLY)
- `src/SkillSwap.API/Program.cs` (Team Lead / Member 4)

---

## 9. Dependencies
- **Consumes:**
  - `ICurrentUserService` (Member 1) to identify caller.
  - Member 2 skill checks (verify `ReceiverId` actively teaches `SkillId`).
  - Member 5 block checks (verify neither user has blocked the other).
- **Supplies to:**
  - Member 4 (`SessionBookingService` requires an accepted `SwapRequest` to schedule sessions).

---

## 10. Cross-Module Contracts

1. **Ironclad Requester/Receiver Role Contract:**
   - **`SwapRequest.RequesterId`** = **Learner** (the user who wants to learn the skill).
   - **`SwapRequest.ReceiverId`** = **Teacher** (the user who offers the skill).
   - **`SwapRequest.SkillId`** = The specific skill to be taught/learned.
   - **Never invert these roles.** Any attempt by `Receiver` to book as learner is rejected.
2. **One SwapRequest to Multiple Sessions:**
   - Once a `SwapRequest` enters status `Accepted`, it serves as a persistent authorization parent for scheduling sessions.
   - **One accepted SwapRequest may spawn multiple Sessions over time.**
   - The SwapRequest does **not** close or transition to `Completed`.
3. **Automatic Conversation Provisioning:**
   - When `AcceptSwapRequestAsync` executes successfully:
     1. Set `SwapRequest.Status = Accepted`.
     2. Create `Conversation` (`SwapRequestId = swapRequest.Id`).
     3. Add two `ConversationParticipant` records (`UserId = RequesterId` and `UserId = ReceiverId`).
     4. Save changes atomically.

---

## 11. Business Rules I Must Respect

1. **No Self-Swaps:** `RequesterId` cannot equal `ReceiverId`.
2. **Teacher Qualification:** `ReceiverId` must possess an active `UserSkill` where `SkillId = request.SkillId` and `Type = Teach`.
3. **Duplicate Request Prevention:** A user cannot have multiple active `Pending` swap requests for the exact same skill with the same receiver.
4. **State Machine Invariants:**
   - `Pending` can transition to `Accepted`, `Declined`, `Cancelled`, or `Expired`.
   - `Accepted`, `Declined`, `Cancelled`, `Expired` are terminal states for the request itself.
   - Only `Requester` can cancel a pending request.
   - Only `Receiver` can accept or decline a pending request.
5. **Request Expiration:** Pending swap requests expire after **7 days** if unresponded (`ExpiresAtUtc = CreatedAtUtc.AddDays(7)`).

---

## 12. Planned Endpoints

| Method | Route | Description | Auth Required | Role |
|---|---|---|---|---|
| `POST` | `/api/swap-requests` | Submit new swap proposal to a teacher | Yes | Authenticated |
| `GET` | `/api/swap-requests/incoming` | List pending/accepted requests where caller is teacher | Yes | Authenticated |
| `GET` | `/api/swap-requests/outgoing` | List pending/accepted requests where caller is learner | Yes | Authenticated |
| `GET` | `/api/swap-requests/{id}` | Get swap request details by ID | Yes | Authenticated |
| `POST` | `/api/swap-requests/{id}/accept` | Teacher accepts request (creates Conversation) | Yes | Authenticated |
| `POST` | `/api/swap-requests/{id}/decline` | Teacher declines swap request | Yes | Authenticated |
| `POST` | `/api/swap-requests/{id}/cancel` | Learner cancels pending swap request | Yes | Authenticated |
| `GET` | `/api/conversations` | List user's active conversations | Yes | Authenticated |
| `GET` | `/api/conversations/{id}/messages` | Get paginated messages in conversation | Yes | Authenticated |
| `POST` | `/api/conversations/{id}/messages` | Post message via REST API (fallback) | Yes | Authenticated |
| `POST` | `/api/conversations/{id}/read` | Update `LastReadMessageId` for caller | Yes | Authenticated |

---

## 13. Existing Implemented Endpoints
- `ChatHub` (`/hubs/chat`): Foundation mapping in `Program.cs` is implemented. Hub methods (`SendMessage`, `JoinConversation`) are currently TODO and will be implemented by Member 3.

---

## 14. Events / SignalR / Notifications

### SignalR Hub Architecture (`ChatHub`):
- Clients connect to `/hubs/chat` with JWT bearer token.
- Hub method `JoinConversation(long conversationId)`:
  - Validates caller is a participant in `ConversationParticipants`.
  - Adds connection to SignalR group: `$"conversation_{conversationId}"`.
- Hub method `SendMessage(long conversationId, string body)`:
  - Validates participant authorization.
  - Inserts and saves `Message` entity in database.
  - Broadcasts to group: `Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", messageDto)`.
- Emits `NotificationKind.SwapRequest` notification when request is proposed (via Member 5).
- Emits `NotificationKind.NewMessage` notification when message is sent to an offline participant.

---

## 15. Database Responsibility
- Tables: `SwapRequests`, `Conversations`, `ConversationParticipants`, `Messages`.
- Composite primary key on `ConversationParticipants`: `(ConversationId, UserId)`.
- Indexes:
  - `IX_SwapRequests_RequesterId_Status`
  - `IX_SwapRequests_ReceiverId_Status`
  - `IX_Messages_ConversationId_SentAtUtc`
- Coordinate migrations with Member 4.

---

## 16. Validation & Authorization
- `CreateSwapRequest`: ReceiverId != CallerId, SkillId > 0, Message max 1000 chars.
- `SendMessageRequest`: ConversationId > 0, Body not empty, max 4000 chars.
- Strict authorization: Only participants can read messages or broadcast in a conversation group.

---

## 17. Testing Requirements
- **Unit Tests:**
  - Transitioning a non-pending request throws `InvalidOperationException` or returns failure Result.
  - Non-receiver attempting to accept request returns 403 / unauthorized.
  - Accepting request correctly provisions Conversation and two participants.
- **Integration Tests:**
  - `ChatHub` rejects unauthenticated WebSocket connections.
  - Caller not in conversation group is denied message history.

---

## 18. Definition of Done
- [ ] SwapRequest negotiation workflow functional via REST APIs.
- [ ] Conversation creation tested and operational upon request acceptance.
- [ ] `ChatHub` methods implemented, secured, and broadcasting to SignalR groups.
- [ ] Role contract (Requester=Learner, Receiver=Teacher) strictly verified.
- [ ] 100% test pass rate on `dotnet test`.
- [ ] PR reviewed and approved by Member 4 and one other team member.

---

## 19. PR Checklist
- [ ] Does `dotnet test` pass with 0 errors?
- [ ] Is SwapRequest protected from receiving a `Completed` status?
- [ ] Are messages saved in DB before SignalR broadcasting?
- [ ] Is group name formatted as `conversation_{conversationId}`?
- [ ] Does the branch follow `feature/swap-chat`?

---

## 20. Common Mistakes to Avoid
- **Mistake 1:** Adding a `Completed` status to `SwapRequestStatus`. (SwapRequest stays `Accepted`; `Session` is what completes).
- **Mistake 2:** Confusing `Requester` and `Receiver` roles. (`Requester` is always `Learner`).
- **Mistake 3:** Broadcasting SignalR messages before database persistence succeeds.
- **Mistake 4:** Allowing non-participants to read messages by omitting caller authorization checks.

---
*Member 3 Ownership Guide -- SkillSwap Backend Team*
