using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.BackendApiClient;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Endpoints;

/// <summary>
/// The handset itself: provisioning, power, PIN, apps, and the two enforcement actions.
///
/// These are the only phone routes that name an acting character. Possession is proven once, when
/// the phone is switched on — the Central API's guard chain then requires a powered-on phone for
/// every app operation, so contacts and messages (see <see cref="PhoneContactEndpoints"/> and
/// <see cref="PhoneMessageEndpoints"/>) address a phone by id alone.
/// </summary>
public static class PhoneEndpoints
{
    public static WebApplication MapPhoneEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Phone");

        group.MapPost("phones", async (
                ProvisionPhoneRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones.PostAsync(
                        new ApiModels.ProvisionPhoneRequestDto { CharacterId = request.CharacterId, Pin = request.Pin },
                        cancellationToken: cancellationToken),
                    phone => phone is null
                        ? Results.Problem("Central API returned an empty provisioning response.")
                        : Results.Ok(new ProvisionedPhone(phone.PhoneId!.Value, phone.Number!)));
            })
            .Produces<ProvisionedPhone>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithName("ProvisionPhone")
            .WithDescription("Provisions a phone with a fresh number and a PIN, registered to a character. Ships powered off, with every app installed. The PIN is 4-8 digits and is never readable back.");

        group.MapGet("characters/{characterId:guid}/phones", async (
                Guid characterId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Characters[characterId].Phones.GetAsync(cancellationToken: cancellationToken),
                    phones => Results.Ok(phones?.Select(PhoneSummary.Create).ToList() ?? []));
            })
            .Produces<IEnumerable<PhoneSummary>>()
            .WithName("ListCharacterPhones")
            .WithDescription("Lists the phones registered to a character. A character may hold several, each with its own number.");

        group.MapGet("phones/{phoneId:guid}", async (
                Guid phoneId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].GetAsync(cancellationToken: cancellationToken),
                    phone => phone is null
                        ? Results.Problem("Central API returned an empty phone response.")
                        : Results.Ok(PhoneSummary.Create(phone)));
            })
            .Produces<PhoneSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetPhone")
            .WithDescription("Gets a phone: its number, status, power state, blocklist and installed apps. Never its PIN.");

        group.MapPost("phones/{phoneId:guid}/power", async (
                Guid phoneId,
                SetPhonePowerRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Power.PostAsync(
                        new ApiModels.SetPhonePowerRequestDto
                        {
                            CharacterId = request.CharacterId,
                            IsPoweredOn = request.IsPoweredOn,
                            Pin = request.Pin,
                        },
                        cancellationToken: cancellationToken),
                    power => power is null
                        ? Results.Problem("Central API returned an empty power response.")
                        : Results.Ok(new PhonePowerState(power.IsPoweredOn ?? false)));
            })
            .Produces<PhonePowerState>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("SetPhonePower")
            .WithDescription("Powers a phone on or off, and is where possession is checked: the owner acts freely, anyone else must send the PIN. Powering on delivers anything queued for the number. Repeating a call is not an error.");

        group.MapPost("phones/{phoneId:guid}/pin", async (
                Guid phoneId,
                ChangePhonePinRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Pin.PostAsync(
                        new ApiModels.ChangePinRequestDto
                        {
                            CharacterId = request.CharacterId,
                            NewPin = request.NewPin,
                            Pin = request.Pin,
                        },
                        cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("ChangePhonePin")
            .WithDescription("Sets a new PIN. Takes the owner, or the current PIN from whoever else is holding the phone — so whoever picks one up and knows the code can lock the previous owner out.");

        group.MapGet("phones/{phoneId:guid}/apps", async (
                Guid phoneId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps.GetAsync(cancellationToken: cancellationToken),
                    apps => Results.Ok(apps?.Select(PhoneApp.Create).ToList() ?? []));
            })
            .Produces<IEnumerable<PhoneApp>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("ListPhoneApps")
            .WithDescription("Lists the apps installed on a phone.");

        group.MapPut("phones/{phoneId:guid}/apps/{appKey}", async (
                Guid phoneId,
                string appKey,
                PhoneActorRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps[appKey].PutAsync(
                        new ApiModels.PhoneActorRequestDto { CharacterId = request.CharacterId, Pin = request.Pin },
                        cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("InstallPhoneApp")
            .WithDescription("Installs an app. Every phone can run every app; installing Messages delivers whatever queued while it was gone. Idempotent.");

        group.MapDelete("phones/{phoneId:guid}/apps/{appKey}", async (
                Guid phoneId,
                string appKey,
                Guid characterId,
                string? pin,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Apps[appKey].DeleteAsync(
                        configuration =>
                        {
                            configuration.QueryParameters.CharacterId = characterId;
                            configuration.QueryParameters.Pin = pin;
                        },
                        cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("UninstallPhoneApp")
            .WithDescription("Uninstalls an app. Idempotent, and nothing is lost — contacts and threads belong to the phone, and messages queue rather than vanish.");

        group.MapPost("phones/{phoneId:guid}/suspend", async (
                Guid phoneId,
                SuspendPhoneRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Suspend.PostAsync(
                        new ApiModels.SuspendPhoneRequestDto { Reason = request.Reason },
                        cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone)
            .WithName("SuspendPhone")
            .WithDescription("Locks a number from outside its owner's control: it can neither send nor receive, and messages to it are dropped rather than queued. Takes no acting character and no PIN — the point of an enforcement action is that the holder does not consent. Nothing stored is lost.");

        group.MapPost("phones/{phoneId:guid}/restore", async (
                Guid phoneId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Phones[phoneId].Restore.PostAsync(cancellationToken: cancellationToken),
                    _ => Results.Ok());
            })
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("RestorePhone")
            .WithDescription("Lifts a suspension, handing the number back whole with its blocklist intact. A deactivated phone stays retired.");

        return app;
    }
}

public sealed record ProvisionPhoneRequest(Guid CharacterId, string Pin);

public sealed record ProvisionedPhone(Guid PhoneId, string Number);

public sealed record SetPhonePowerRequest(Guid CharacterId, bool IsPoweredOn, string? Pin = null);

public sealed record PhonePowerState(bool IsPoweredOn);

public sealed record ChangePhonePinRequest(Guid CharacterId, string NewPin, string? Pin = null);

/// <summary>The acting character for a platform command, plus the PIN when they are not the owner.</summary>
public sealed record PhoneActorRequest(Guid CharacterId, string? Pin = null);

public sealed record SuspendPhoneRequest(string Reason);

public sealed record PhoneApp(string Key, string DisplayName)
{
    public static PhoneApp Create(ApiModels.PhoneAppDto source) => new(source.Key!, source.DisplayName!);
}

/// <summary>
/// Note what is absent: the PIN. The Central API never returns it on any read, and neither does this
/// — a response that echoed it would hand it to anyone who can reach the Bridge.
/// </summary>
public sealed record PhoneSummary(
    Guid PhoneId,
    string Number,
    Guid RegisteredTo,
    PhoneStatus Status,
    bool IsPoweredOn,
    IReadOnlyList<string> BlockedNumbers,
    IReadOnlyList<string> InstalledApps)
{
    public static PhoneSummary Create(ApiModels.PhoneDto source) => new(
        source.Id!.Value,
        source.Number!,
        source.RegisteredTo!.Value,
        FromCentralApi(source.Status),
        source.IsPoweredOn ?? false,
        source.BlockedNumbers ?? [],
        source.InstalledApps ?? []);

    private static PhoneStatus FromCentralApi(string? status) => status switch
    {
        "Active" => PhoneStatus.Active,
        "Suspended" => PhoneStatus.Suspended,
        "Deactivated" => PhoneStatus.Deactivated,
        _ => PhoneStatus.Unknown,
    };
}

/// <summary>
/// Whether a number is usable. Suspended is reported distinctly from Deactivated so the mod can tell
/// "locked, and it may come back" from "retired for good". Member names match the strings Core emits.
/// </summary>
public enum PhoneStatus
{
    /// <summary>Core sent a status this build does not know. See PlayerSessionStatus.Unknown.</summary>
    Unknown = 0,

    Active,

    Suspended,

    Deactivated,
}
