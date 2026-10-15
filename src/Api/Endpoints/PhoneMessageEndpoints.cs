using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.BackendApiClient;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Endpoints;

/// <summary>
/// The Messages app: conversations, sending, and the polling feed the mod actually lives on.
///
/// No acting character and no PIN, for the reason given in <see cref="PhoneContactEndpoints"/> — the
/// Central API's guard chain requires a powered-on phone, and powering it on is where possession was
/// proven.
/// </summary>
public static class PhoneMessageEndpoints
{
    public static WebApplication MapPhoneMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Phone");

        group.MapGet("phones/{phoneId:guid}/apps/messages/updates", async (
                Guid phoneId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Messages.Updates.GetAsync(cancellationToken: cancellationToken),
                    updates => updates is null
                        ? Results.Problem("Central API returned an empty updates response.")
                        : Results.Ok(MessageUpdates.Create(updates)));
            })
            .Produces<MessageUpdates>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("PollPhoneMessages")
            .WithDescription("Reports every message this phone has not yet retrieved. There is no cursor to hold any more: retrieval is a watermark the Central API tracks per thread, and only an ack advances it — so an unacked poll repeats. Delivery is at-least-once, so dedupe on `messageId`. This is the mod's delivery path: the Central API also pushes over SignalR, which Reforger cannot consume.");

        group.MapPost("phones/{phoneId:guid}/apps/messages/updates/ack", async (
                Guid phoneId,
                AckPhoneMessagesRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Messages.Updates.Ack.PostAsync(
                        new ApiModels.AckMessageUpdatesRequestDto
                        {
                            Threads =
                            [
                                .. request.Threads.Select(thread => new ApiModels.AckThreadDto
                                {
                                    ThreadId = thread.ThreadId,
                                    ThroughSequence = thread.ThroughSequence.ToUntypedNode(),
                                }),
                            ],
                        },
                        cancellationToken: cancellationToken),
                    acked => acked is null
                        ? Results.Problem("Central API returned an empty ack response.")
                        : Results.Ok(AckedPhoneMessages.Create(acked)));
            })
            .Produces<AckedPhoneMessages>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("AckPhoneMessages")
            .WithDescription("Advances the retrieval watermark to the `highestSequence` you finished processing, per thread. Leave a thread off to have it redelivered; re-sending the same ack is a no-op. Acking needs the phone powered on, so a power-cycle mid-batch redelivers rather than loses.");

        group.MapGet("phones/{phoneId:guid}/apps/messages/threads", async (
                Guid phoneId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Messages.Threads.GetAsync(cancellationToken: cancellationToken),
                    threads => Results.Ok(threads?.Select(MessageThreadSummary.Create).ToList() ?? []));
            })
            .Produces<IEnumerable<MessageThreadSummary>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("ListPhoneThreads")
            .WithDescription("Lists a phone's conversations, newest first. Bodies are omitted — open a thread for those.");

        group.MapGet("phones/{phoneId:guid}/apps/messages/threads/{threadId:guid}", async (
                Guid phoneId,
                Guid threadId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Messages.Threads[threadId].GetAsync(cancellationToken: cancellationToken),
                    thread => thread is null
                        ? Results.Problem("Central API returned an empty thread response.")
                        : Results.Ok(MessageThreadDetail.Create(thread)));
            })
            .Produces<MessageThreadDetail>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("GetPhoneThread")
            .WithDescription("Gets one conversation with its retained messages. This is the source of truth a poller re-reads after a gap.");

        group.MapPost("phones/{phoneId:guid}/apps/messages/send", async (
                Guid phoneId,
                SendPhoneMessageRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Messages.Send.PostAsync(
                        new ApiModels.SendMessageRequestDto { To = [.. request.To], Body = request.Body },
                        cancellationToken: cancellationToken),
                    sent => sent is null
                        ? Results.Problem("Central API returned an empty send response.")
                        : Results.Ok(SentMessage.Create(sent)));
            })
            .Produces<SentMessage>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("SendPhoneMessage")
            .WithDescription("Sends a message to one or more numbers. Unknown, suspended and retired numbers come back in `undeliverableRecipients`; blocked ones deliberately do not, so a sender cannot detect a block.");

        group.MapPost("phones/{phoneId:guid}/apps/messages/threads/{threadId:guid}/read", async (
                Guid phoneId,
                Guid threadId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Messages.Threads[threadId].Read.PostAsync(cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("MarkPhoneThreadRead")
            .WithDescription("Clears a conversation's unread count. Polling never does this on its own.");

        group.MapPost("phones/{phoneId:guid}/apps/messages/blocks", async (
                Guid phoneId,
                BlockPhoneNumberRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Messages.Blocks.PostAsync(
                        new ApiModels.BlockNumberRequestDto { Number = request.Number },
                        cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("BlockPhoneNumber")
            .WithDescription("Blocks a number. Messages from it are dropped silently — the sender still sees them as sent. Idempotent.");

        group.MapDelete("phones/{phoneId:guid}/apps/messages/blocks/{number}", async (
                Guid phoneId,
                string number,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Messages.Blocks[number].DeleteAsync(cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("UnblockPhoneNumber")
            .WithDescription("Removes a number from the blocklist. Idempotent.");

        return app;
    }
}

public sealed record SendPhoneMessageRequest(IReadOnlyList<string> To, string Body);

public sealed record BlockPhoneNumberRequest(string Number);

/// <summary>
/// <paramref name="UndeliverableRecipients"/> reports only what a real network would reveal: numbers
/// that do not exist, or are suspended or retired. A blocked recipient is deliberately absent.
/// </summary>
public sealed record SentMessage(Guid ThreadId, Guid MessageId, IReadOnlyList<string> UndeliverableRecipients)
{
    public static SentMessage Create(ApiModels.SendMessageResponseDto source) => new(
        source.ThreadId!.Value,
        source.MessageId!.Value,
        source.UndeliverableRecipients ?? []);
}

public sealed record PhoneMessage(Guid MessageId, string From, string Body, DateTimeOffset SentAt, bool IsOutbound)
{
    public static PhoneMessage Create(ApiModels.MessageDto source) => new(
        source.Id!.Value,
        source.From!,
        source.Body!,
        source.SentAt!.Value,
        source.IsOutbound ?? false);
}

public sealed record MessageThreadSummary(
    Guid ThreadId,
    IReadOnlyList<string> Participants,
    int UnreadCount,
    DateTimeOffset LastMessageAt)
{
    public static MessageThreadSummary Create(ApiModels.MessageThreadSummaryDto source) => new(
        source.Id!.Value,
        source.Participants ?? [],
        // UnreadCount crosses as an UntypedNode, not an int — see UntypedNodeExtensions for why
        // .GetValue() cannot be called on it directly.
        source.UnreadCount?.ToInt32() ?? 0,
        source.LastMessageAt!.Value);
}

public sealed record MessageThreadDetail(
    Guid ThreadId,
    IReadOnlyList<string> Participants,
    int UnreadCount,
    DateTimeOffset LastMessageAt,
    IReadOnlyList<PhoneMessage> Messages)
{
    public static MessageThreadDetail Create(ApiModels.MessageThreadDto source) => new(
        source.Id!.Value,
        source.Participants ?? [],
        source.UnreadCount?.ToInt32() ?? 0,
        source.LastMessageAt!.Value,
        source.Messages?.Select(PhoneMessage.Create).ToList() ?? []);
}

/// <summary>
/// One thread as a poll reports it: <paramref name="Messages"/> carries only what this phone has not
/// retrieved, not the thread's whole history.
///
/// <paramref name="HighestSequence"/> is what you ack once the batch is processed, and
/// <paramref name="RetrievedThrough"/> is where the watermark stood when the poll was answered —
/// together they say exactly what this poll is asking you to take responsibility for.
/// </summary>
public sealed record MessageThreadUpdate(
    Guid ThreadId,
    IReadOnlyList<string> Participants,
    int UnreadCount,
    DateTimeOffset LastMessageAt,
    int RetrievedThrough,
    int HighestSequence,
    IReadOnlyList<PhoneMessage> Messages)
{
    public static MessageThreadUpdate Create(ApiModels.MessageThreadUpdateDto source) => new(
        source.Id!.Value,
        source.Participants ?? [],
        source.UnreadCount?.ToInt32() ?? 0,
        source.LastMessageAt!.Value,
        source.RetrievedThrough?.ToInt32() ?? 0,
        source.HighestSequence?.ToInt32() ?? 0,
        source.Messages?.Select(PhoneMessage.Create).ToList() ?? []);
}

/// <summary>
/// No cursor: what to send back is a per-thread sequence, through
/// <c>POST .../apps/messages/updates/ack</c>. A caller that acks nothing is handed the same batch
/// again, which is the safe direction.
/// </summary>
public sealed record MessageUpdates(IReadOnlyList<MessageThreadUpdate> Threads)
{
    public static MessageUpdates Create(ApiModels.MessageUpdatesDto source) => new(
        source.Threads?.Select(MessageThreadUpdate.Create).ToList() ?? []);
}

/// <summary>One thread's high-water sequence the caller has finished processing.</summary>
public sealed record AckPhoneMessagesThread(Guid ThreadId, int ThroughSequence);

public sealed record AckPhoneMessagesRequest(IReadOnlyList<AckPhoneMessagesThread> Threads);

/// <summary>
/// <paramref name="Threads"/> echoes each acked thread's watermark as it stands after the clamp, so
/// the effective value is visible without a second poll. <paramref name="UnknownThreadIds"/> lists
/// any thread id that does not belong to this phone.
/// </summary>
public sealed record AckedPhoneMessages(
    IReadOnlyList<AckedPhoneMessagesThread> Threads,
    IReadOnlyList<Guid> UnknownThreadIds)
{
    public static AckedPhoneMessages Create(ApiModels.AckMessageUpdatesResponseDto source) => new(
        source.Threads?.Select(AckedPhoneMessagesThread.Create).ToList() ?? [],
        [.. source.UnknownThreadIds?.Where(id => id.HasValue).Select(id => id!.Value) ?? []]);
}

public sealed record AckedPhoneMessagesThread(Guid ThreadId, int RetrievedThrough)
{
    public static AckedPhoneMessagesThread Create(ApiModels.AckThreadWatermarkDto source) => new(
        source.ThreadId!.Value,
        source.RetrievedThrough?.ToInt32() ?? 0);
}
