using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class ChatServiceTests
{
    private (ChatService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc) };
        var service = new ChatService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task GetUserConversations_ReturnsConversationsWithUnreadCountAndOtherParticipant()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = userA, DisplayName = "User A" },
            new UserProfile { UserId = userB, DisplayName = "User B" }
        );

        var cat = new Category { Name = "Tech", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Docker", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var swapRequest = new SwapRequest
        {
            RequesterId = userA,
            ReceiverId = userB,
            SkillId = skill.Id,
            Status = SwapRequestStatus.Accepted,
            CreatedAtUtc = dtp.UtcNow.AddDays(-2)
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var conversation = new Conversation
        {
            SwapRequestId = swapRequest.Id,
            CreatedAtUtc = dtp.UtcNow.AddDays(-2),
            Participants =
            [
                new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow.AddDays(-2), LastReadMessageId = 1 },
                new ConversationParticipant { UserId = userB, JoinedAtUtc = dtp.UtcNow.AddDays(-2) }
            ]
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        // Add messages: 1 read by userA, 2 unread from userB
        context.Messages.AddRange(
            new Message { Id = 1, ConversationId = conversation.Id, SenderId = userB, Body = "Hi", SentAtUtc = dtp.UtcNow.AddHours(-2) },
            new Message { Id = 2, ConversationId = conversation.Id, SenderId = userB, Body = "Are you ready?", SentAtUtc = dtp.UtcNow.AddHours(-1) },
            new Message { Id = 3, ConversationId = conversation.Id, SenderId = userB, Body = "Let's schedule!", SentAtUtc = dtp.UtcNow.AddMinutes(-30) }
        );
        await context.SaveChangesAsync();

        var result = await service.GetUserConversationsAsync(userA);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        var convDto = result.Value![0];
        Assert.Equal(conversation.Id, convDto.Id);
        Assert.Equal(userB, convDto.OtherParticipantId);
        Assert.Equal("User B", convDto.OtherParticipantDisplayName);
        Assert.Equal("Docker", convDto.SkillName);
        Assert.Equal("Let's schedule!", convDto.LastMessageBody);
        Assert.Equal(2, convDto.UnreadCount); // Messages 2 and 3 are unread
    }

    [Fact]
    public async Task GetConversationById_WhenNonParticipant_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var outsider = Guid.NewGuid();

        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants =
            [
                new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow },
                new ConversationParticipant { UserId = userB, JoinedAtUtc = dtp.UtcNow }
            ]
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var result = await service.GetConversationByIdAsync(outsider, conversation.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("not a participant", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetConversationMessages_WhenAuthorized_ReturnsMessages()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = userA, DisplayName = "Alice" },
            new UserProfile { UserId = userB, DisplayName = "Bob" }
        );

        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants =
            [
                new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow },
                new ConversationParticipant { UserId = userB, JoinedAtUtc = dtp.UtcNow }
            ]
        };
        context.Conversations.Add(conversation);

        context.Messages.AddRange(
            new Message { ConversationId = conversation.Id, SenderId = userA, Body = "Hello", SentAtUtc = dtp.UtcNow.AddMinutes(-10) },
            new Message { ConversationId = conversation.Id, SenderId = userB, Body = "Hi there", SentAtUtc = dtp.UtcNow.AddMinutes(-5) }
        );
        await context.SaveChangesAsync();

        var result = await service.GetConversationMessagesAsync(userA, conversation.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("Hello", result.Value[0].Body);
        Assert.Equal("Alice", result.Value[0].SenderDisplayName);
        Assert.Equal("Hi there", result.Value[1].Body);
        Assert.Equal("Bob", result.Value[1].SenderDisplayName);
    }

    [Fact]
    public async Task GetConversationMessages_WhenNonParticipant_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var outsider = Guid.NewGuid();

        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants =
            [
                new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow },
                new ConversationParticipant { UserId = userB, JoinedAtUtc = dtp.UtcNow }
            ]
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var result = await service.GetConversationMessagesAsync(outsider, conversation.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("not a participant", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessage_ValidMessage_PersistsMessageAndUpdatesReadCursor()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = userA, DisplayName = "Sender Alice" },
            new UserProfile { UserId = userB, DisplayName = "Receiver Bob" }
        );

        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants =
            [
                new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow },
                new ConversationParticipant { UserId = userB, JoinedAtUtc = dtp.UtcNow }
            ]
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var result = await service.SendMessageAsync(userA, conversation.Id, "Let's start the session!");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Let's start the session!", result.Value.Body);
        Assert.Equal(userA, result.Value.SenderId);
        Assert.Equal("Sender Alice", result.Value.SenderDisplayName);
        Assert.Equal(dtp.UtcNow, result.Value.SentAtUtc);

        // Verify message was stored in database
        var persisted = context.Messages.FirstOrDefault(m => m.ConversationId == conversation.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Let's start the session!", persisted.Body);

        // Verify sender's LastReadMessageId was automatically updated to this message
        var participantA = context.ConversationParticipants.First(cp => cp.ConversationId == conversation.Id && cp.UserId == userA);
        Assert.Equal(persisted.Id, participantA.LastReadMessageId);
    }

    [Fact]
    public async Task SendMessage_WhenNonParticipant_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var intruder = Guid.NewGuid();

        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants =
            [
                new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow },
                new ConversationParticipant { UserId = userB, JoinedAtUtc = dtp.UtcNow }
            ]
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var result = await service.SendMessageAsync(intruder, conversation.Id, "Spam message");

        Assert.True(result.IsFailure);
        Assert.Contains("not a participant", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessage_EmptyOrWhitespaceBody_ReturnsFailure(string body)
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants = [new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow }]
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var result = await service.SendMessageAsync(userA, conversation.Id, body);

        Assert.True(result.IsFailure);
        Assert.Contains("cannot be empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessage_BodyExceeds2000Characters_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants = [new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow }]
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var hugeBody = new string('x', 2001);
        var result = await service.SendMessageAsync(userA, conversation.Id, hugeBody);

        Assert.True(result.IsFailure);
        Assert.Contains("2000 characters", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessage_WhenBlockedInEitherDirection_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants =
            [
                new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow },
                new ConversationParticipant { UserId = userB, JoinedAtUtc = dtp.UtcNow }
            ]
        };
        context.Conversations.Add(conversation);

        // userB blocked userA
        context.UserBlocks.Add(new UserBlock { BlockerId = userB, BlockedId = userA });
        await context.SaveChangesAsync();

        var result = await service.SendMessageAsync(userA, conversation.Id, "Blocked message");

        Assert.True(result.IsFailure);
        Assert.Contains("block", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkConversationAsRead_ValidParticipantAndMessage_UpdatesLastReadMessageId()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants = [new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow, LastReadMessageId = null }]
        };
        context.Conversations.Add(conversation);

        var msg = new Message { Id = 42, ConversationId = conversation.Id, SenderId = Guid.NewGuid(), Body = "Text", SentAtUtc = dtp.UtcNow };
        context.Messages.Add(msg);
        await context.SaveChangesAsync();

        var result = await service.MarkConversationAsReadAsync(userA, conversation.Id, 42);

        Assert.True(result.IsSuccess);
        var participant = context.ConversationParticipants.First(cp => cp.ConversationId == conversation.Id && cp.UserId == userA);
        Assert.Equal(42, participant.LastReadMessageId);
    }

    [Fact]
    public async Task MarkConversationAsRead_MessageNotFromConversation_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var userA = Guid.NewGuid();
        var conversation = new Conversation
        {
            SwapRequestId = 1,
            CreatedAtUtc = dtp.UtcNow,
            Participants = [new ConversationParticipant { UserId = userA, JoinedAtUtc = dtp.UtcNow }]
        };
        var otherConversation = new Conversation
        {
            SwapRequestId = 2,
            CreatedAtUtc = dtp.UtcNow
        };
        context.Conversations.AddRange(conversation, otherConversation);

        var otherMsg = new Message { Id = 99, ConversationId = otherConversation.Id, SenderId = Guid.NewGuid(), Body = "Other", SentAtUtc = dtp.UtcNow };
        context.Messages.Add(otherMsg);
        await context.SaveChangesAsync();

        var result = await service.MarkConversationAsReadAsync(userA, conversation.Id, 99);

        Assert.True(result.IsFailure);
        Assert.Contains("does not belong", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
