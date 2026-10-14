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
    public void MessageUpdates_Create_CarriesTheSequencesAndOnlyTheNewMessages()
    {
        var sentAt = DateTimeOffset.UnixEpoch.AddMinutes(5);
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var updates = MessageUpdates.Create(new ApiModels.MessageUpdatesDto
        {
            Threads =
            [
                new ApiModels.MessageThreadUpdateDto
                {
                    Id = threadId,
                    Participants = ["12345678"],
                    UnreadCount = new UntypedInteger(1),
                    LastMessageAt = sentAt,
                    RetrievedThrough = new UntypedInteger(3),
                    HighestSequence = new UntypedInteger(4),
                    Messages =
                    [
                        new ApiModels.MessageDto
                        {
                            Id = messageId,
                            From = "12345678",
                            Body = "second",
                            SentAt = sentAt,
                            IsOutbound = false,
                        },
                    ],
                },
            ],
        });

        var thread = Assert.Single(updates.Threads);
        Assert.Equal(threadId, thread.ThreadId);
        Assert.Equal(1, thread.UnreadCount);

        // These two are the protocol now that there is no cursor: HighestSequence is what the caller
        // acks, RetrievedThrough is where the watermark stood when the poll was answered.
        Assert.Equal(3, thread.RetrievedThrough);
        Assert.Equal(4, thread.HighestSequence);

        var message = Assert.Single(thread.Messages);
        Assert.Equal(messageId, message.MessageId);
        Assert.Equal("second", message.Body);
        Assert.False(message.IsOutbound);
    }

    [Fact]
    public void MessageUpdates_Create_WithNothingNew_YieldsNoThreads()
    {
        var updates = MessageUpdates.Create(new ApiModels.MessageUpdatesDto { Threads = null });

        Assert.Empty(updates.Threads);
    }

    [Fact]
    public void AckedPhoneMessages_Create_EchoesWatermarksAndDropsNullUnknownIds()
    {
        var threadId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();

        var acked = AckedPhoneMessages.Create(new ApiModels.AckMessageUpdatesResponseDto
        {
            Threads =
            [
                new ApiModels.AckThreadWatermarkDto { ThreadId = threadId, RetrievedThrough = new UntypedInteger(4) },
            ],
            // Kiota types this as List<Guid?>, so the mapper has to drop nulls rather than hand the
            // mod a nullable it cannot represent.
            UnknownThreadIds = [unknownId, null],
        });

        var watermark = Assert.Single(acked.Threads);
        Assert.Equal(threadId, watermark.ThreadId);
        Assert.Equal(4, watermark.RetrievedThrough);
        Assert.Equal(unknownId, Assert.Single(acked.UnknownThreadIds));
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

        var app = PhoneApp.Create(new ApiModels.PhoneAppDto
        {
            Key = "Messages",
            DisplayName = "Messages",
            IsSystemApp = true,
        });

        Assert.Equal("Messages", app.Key);
        Assert.Equal("Messages", app.DisplayName);
        Assert.True(app.IsSystemApp);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StoreApp_Create_MapsTheInstallState(bool installed)
    {
        var app = StoreApp.Create(new ApiModels.StoreAppDto
        {
            Key = "Bank",
            DisplayName = "Bank",
            IsInstalled = installed,
        });

        Assert.Equal("Bank", app.Key);
        Assert.Equal("Bank", app.DisplayName);
        Assert.Equal(installed, app.IsInstalled);
    }

    [Fact]
    public void PhoneApp_And_StoreApp_Create_WithNullFlags_DefaultToFalse()
    {
        // Kiota declares both flags bool?, so an absent field would otherwise NRE rather than
        // default — and "false" is the safe answer for each: not a system app, not installed.
        Assert.False(PhoneApp.Create(new ApiModels.PhoneAppDto { Key = "Bank", DisplayName = "Bank" }).IsSystemApp);
        Assert.False(StoreApp.Create(new ApiModels.StoreAppDto { Key = "Bank", DisplayName = "Bank" }).IsInstalled);
    }
}
