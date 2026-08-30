using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.BackendApiClient;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Endpoints;

/// <summary>
/// The Contacts app's address book. Rooted under the app that owns it, mirroring the Central API.
///
/// No acting character and no PIN: contacts belong to the handset, and the Central API's guard chain
/// already requires the phone to be powered on — which is where possession was proven. See
/// <see cref="PhoneEndpoints"/>.
/// </summary>
public static class PhoneContactEndpoints
{
    public static WebApplication MapPhoneContactEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Phone");

        group.MapGet("phones/{phoneId:guid}/apps/contacts/entries", async (
                Guid phoneId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Contacts.Entries.GetAsync(cancellationToken: cancellationToken),
                    contacts => Results.Ok(contacts?.Select(ContactSummary.Create).ToList() ?? []));
            })
            .Produces<IEnumerable<ContactSummary>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("ListPhoneContacts")
            .WithDescription("Lists a phone's saved contacts.");

        group.MapPost("phones/{phoneId:guid}/apps/contacts/entries", async (
                Guid phoneId,
                SavePhoneContactRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Contacts.Entries.PostAsync(
                        new ApiModels.SaveContactRequestDto { Number = request.Number, DisplayName = request.DisplayName },
                        cancellationToken: cancellationToken),
                    saved => saved is null
                        ? Results.Problem("Central API returned an empty contact response.")
                        : Results.Ok(new SavedContact(saved.ContactId!.Value)));
            })
            .Produces<SavedContact>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("SavePhoneContact")
            .WithDescription("Saves a number to a phone's address book. Numbers may be typed with spaces, dashes, parentheses or a leading +; the Central API canonicalises them.");

        group.MapPatch("phones/{phoneId:guid}/apps/contacts/entries/{contactId:guid}", async (
                Guid phoneId,
                Guid contactId,
                RenamePhoneContactRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Contacts.Entries[contactId].PatchAsync(
                        new ApiModels.RenameContactRequestDto { DisplayName = request.DisplayName },
                        cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("RenamePhoneContact")
            .WithDescription("Renames a saved contact.");

        group.MapDelete("phones/{phoneId:guid}/apps/contacts/entries/{contactId:guid}", async (
                Guid phoneId,
                Guid contactId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.Contacts.Entries[contactId].DeleteAsync(cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("DeletePhoneContact")
            .WithDescription("Removes a saved contact.");

        return app;
    }
}

public sealed record SavePhoneContactRequest(string Number, string DisplayName);

public sealed record SavedContact(Guid ContactId);

public sealed record RenamePhoneContactRequest(string DisplayName);

public sealed record ContactSummary(Guid ContactId, string Number, string DisplayName)
{
    public static ContactSummary Create(ApiModels.ContactDto source) =>
        new(source.Id!.Value, source.Number!, source.DisplayName!);
}
