using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.BackendApiClient;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Endpoints;

/// <summary>
/// The platform notification queue — the APNs/FCM idea for a phone with no OS of its own. Rooted at
/// the phone rather than under an app's prefix, because an app publishes into the queue but owns no
/// part of it.
///
/// It runs the Central API's *device* guard chain rather than the app one, so a banner is readable
/// with the publishing app uninstalled — platform state, not app state — and refused only when the
/// handset itself is unusable: powered off is 409, suspended 403, deactivated 410.
/// </summary>
public static class PhoneNotificationEndpoints
{
    public static WebApplication MapPhoneNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Phone");

        group.MapGet("phones/{phoneId:guid}/notifications", async (
                Guid phoneId,
                string? appKey,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Notifications.GetAsync(
                        configuration => configuration.QueryParameters.AppKey = appKey,
                        cancellationToken),
                    notifications => Results.Ok(
                        notifications?.Notifications?.Select(PhoneNotification.Create).ToList() ?? []));
            })
            .Produces<IEnumerable<PhoneNotification>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("ListPhoneNotifications")
            .WithDescription("Lists a phone's undelivered notifications, oldest first. Pass `appKey` to filter to one app. Nothing is removed by reading — a lost response must not lose a banner — so ack what you have rendered. Readable with the publishing app uninstalled, but refused on a powered-off or enforced phone.");

        group.MapPost("phones/{phoneId:guid}/notifications/ack", async (
                Guid phoneId,
                AckPhoneNotificationsRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Notifications.Ack.PostAsync(
                        new ApiModels.AckNotificationsRequestDto { Ids = [.. request.Ids.Cast<Guid?>()] },
                        cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("AckPhoneNotifications")
            .WithDescription("Removes the given notifications from the queue. Idempotent: an id already acked, or already dropped by the queue cap, is ignored rather than reported, so an ack you are unsure landed can simply be resent.");

        return app;
    }
}

/// <summary>
/// A banner, ready to render without a second call: <paramref name="Title"/> and
/// <paramref name="Body"/> are the text, <paramref name="Category"/> says what kind of event it was
/// (<c>"message.received"</c> today), and <paramref name="GroupKey"/> groups banners that belong
/// together — the thread id, for Messages.
///
/// <paramref name="Title"/> carries a bare phone number rather than a resolved display name: the
/// Central API deliberately does not read Contacts to build one, so resolving it is the mod's job,
/// on the call it already makes to draw a thread.
/// </summary>
public sealed record PhoneNotification(
    Guid NotificationId,
    string AppKey,
    string Category,
    string GroupKey,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Payload,
    DateTimeOffset OccurredAt)
{
    public static PhoneNotification Create(ApiModels.PhoneNotificationDto source) => new(
        source.Id!.Value,
        source.AppKey!,
        source.Category!,
        source.GroupKey!,
        source.Title!,
        source.Body!,
        source.Payload.ToStringDictionary(),
        source.OccurredAt!.Value);
}

public sealed record AckPhoneNotificationsRequest(IReadOnlyList<Guid> Ids);
