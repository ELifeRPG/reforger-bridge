using ELifeRPG.Bridge.Api.Endpoints;
using Microsoft.Kiota.Abstractions.Serialization;
using Xunit;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.UnitTests.Endpoints;

/// <summary>
/// The phone endpoints are thin proxies; the only logic they carry is in these mappers, and two
/// parts of it are easy to get silently wrong: unwrapping the UntypedNode that UnreadCount arrives
/// as, and defaulting the collections Kiota declares nullable.
/// </summary>
public sealed class PhoneMapperTests
{
    [Fact]
    public void PhoneSummary_Create_MapsEveryFieldAndNeverCarriesAPin()
    {
        var phoneId = Guid.NewGuid();
        var owner = Guid.NewGuid();

        var summary = PhoneSummary.Create(new ApiModels.PhoneDto
        {
            Id = phoneId,
            Number = "12345678",
            RegisteredTo = owner,
            Status = "Active",
            IsPoweredOn = true,
            BlockedNumbers = ["87654321"],
            InstalledApps = ["Messages", "Contacts"],
        });

        Assert.Equal(phoneId, summary.PhoneId);
        Assert.Equal("12345678", summary.Number);
        Assert.Equal(owner, summary.RegisteredTo);
        Assert.Equal(PhoneStatus.Active, summary.Status);
        Assert.True(summary.IsPoweredOn);
        Assert.Equal(["87654321"], summary.BlockedNumbers);
        Assert.Equal(["Messages", "Contacts"], summary.InstalledApps);

        // The Central API never returns a PIN on a read, and the Bridge's own shape has nowhere to
        // put one. Asserted as a property of the type so adding a field named Pin fails here.
        Assert.DoesNotContain(
            typeof(PhoneSummary).GetProperties(),
            property => property.Name.Contains("Pin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PhoneSummary_Create_WithNullCollections_YieldsEmptyOnesRatherThanNull()
    {
        var summary = PhoneSummary.Create(new ApiModels.PhoneDto
        {
            Id = Guid.NewGuid(),
            Number = "12345678",
            RegisteredTo = Guid.NewGuid(),
            Status = "Active",
            IsPoweredOn = false,
            BlockedNumbers = null,
            InstalledApps = null,
        });

        Assert.Empty(summary.BlockedNumbers);
        Assert.Empty(summary.InstalledApps);
    }

    [Fact]
    public void MessageThreadSummary_Create_UnwrapsTheUntypedUnreadCount()
    {
        // UnreadCount crosses the wire as an UntypedNode rather than an int, because Kiota does not
        // model OpenAPI's format: int32. Reading it as a plain value throws — see UntypedNodeExtensions.
        var summary = MessageThreadSummary.Create(new ApiModels.MessageThreadSummaryDto
        {
            Id = Guid.NewGuid(),
            Participants = ["12345678"],
            UnreadCount = new UntypedInteger(3),
            LastMessageAt = DateTimeOffset.UnixEpoch,
        });

        Assert.Equal(3, summary.UnreadCount);
    }

    [Fact]
    public void MessageThreadSummary_Create_WithNoUnreadCount_ReadsAsZero()
    {
        var summary = MessageThreadSummary.Create(new ApiModels.MessageThreadSummaryDto
        {
            Id = Guid.NewGuid(),
            Participants = null,
            UnreadCount = null,
            LastMessageAt = DateTimeOffset.UnixEpoch,
        });

        Assert.Equal(0, summary.UnreadCount);
        Assert.Empty(summary.Participants);
    }

    [Fact]
    public void MessageUpdates_Create_CarriesTheCursorAndOnlyTheNewMessages()
    {
        var polledAt = DateTimeOffset.UnixEpoch.AddMinutes(5);
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var updates = MessageUpdates.Create(new ApiModels.MessageUpdatesDto
        {
            PolledAt = polledAt,
            Threads =
            [
                new ApiModels.MessageThreadUpdateDto
                {
                    Id = threadId,
                    Participants = ["12345678"],
                    UnreadCount = new UntypedInteger(1),
                    LastMessageAt = polledAt,
                    Messages =
                    [
                        new ApiModels.MessageDto
                        {
                            Id = messageId,
                            From = "12345678",
                            Body = "second",
                            SentAt = polledAt,
                            IsOutbound = false,
                        },
                    ],
                },
            ],
        });

        // The cursor is the whole protocol: losing it means the next poll re-reads everything.
        Assert.Equal(polledAt, updates.PolledAt);

        var thread = Assert.Single(updates.Threads);
        Assert.Equal(threadId, thread.ThreadId);
        Assert.Equal(1, thread.UnreadCount);

        var message = Assert.Single(thread.Messages);
        Assert.Equal(messageId, message.MessageId);
        Assert.Equal("second", message.Body);
        Assert.False(message.IsOutbound);
    }

    [Fact]
    public void MessageUpdates_Create_WithNothingNew_YieldsNoThreads()
    {
        var updates = MessageUpdates.Create(new ApiModels.MessageUpdatesDto
        {
            PolledAt = DateTimeOffset.UnixEpoch,
            Threads = null,
        });

        Assert.Empty(updates.Threads);
    }

    [Fact]
    public void SentMessage_Create_ReportsUndeliverableRecipients()
    {
        var sent = SentMessage.Create(new ApiModels.SendMessageResponseDto
        {
            ThreadId = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            UndeliverableRecipients = ["00000000"],
        });

        Assert.Equal(["00000000"], sent.UndeliverableRecipients);
    }

    [Fact]
    public void ContactSummary_And_PhoneApp_Create_MapTheirFields()
    {
        var contactId = Guid.NewGuid();
        var contact = ContactSummary.Create(new ApiModels.ContactDto
        {
            Id = contactId,
            Number = "12345678",
            DisplayName = "Dispatcher",
        });

        Assert.Equal(contactId, contact.ContactId);
        Assert.Equal("12345678", contact.Number);
        Assert.Equal("Dispatcher", contact.DisplayName);

        var app = PhoneApp.Create(new ApiModels.PhoneAppDto { Key = "Messages", DisplayName = "Messages" });
        Assert.Equal("Messages", app.Key);
        Assert.Equal("Messages", app.DisplayName);
    }
}
