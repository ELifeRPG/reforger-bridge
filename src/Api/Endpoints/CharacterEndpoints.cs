using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.Bridge.Api.Services;
using ELifeRPG.BackendApiClient;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Endpoints;

public static class CharacterEndpoints
{
    public static WebApplication MapCharacterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Character");

        group.MapPost("characters", async (
                CreateCharacterRequest request,
                EliferpgApiClient apiClient,
                PlayerSessionTracker sessions,
                CancellationToken cancellationToken) =>
            {
                var resolution = sessions.ResolveAccountId(request.BohemiaId);
                if (resolution.Error is not null)
                {
                    return resolution.Error;
                }

                return await ApiCallExtensions.ExecuteAsync(
                    () => apiClient.Api.Characters.PostAsync(
                        new ApiModels.CreateCharacterRequestDto { AccountId = resolution.AccountId, Name = request.Name },
                        cancellationToken: cancellationToken),
                    character => character is null
                        ? Results.Problem("Central API returned an empty character response.")
                        : Results.Ok(CharacterSummary.Create(character)));
            })
            .WithName("CreateCharacter")
            .WithDescription("Creates a new character for the connected player.");

        group.MapGet("characters", async (
                Guid bohemiaId,
                EliferpgApiClient apiClient,
                PlayerSessionTracker sessions,
                CancellationToken cancellationToken) =>
            {
                var resolution = sessions.ResolveAccountId(bohemiaId);
                if (resolution.Error is not null)
                {
                    return resolution.Error;
                }

                var characters = await apiClient.Api.Accounts[resolution.AccountId!.Value].Characters.GetAsync(cancellationToken: cancellationToken);
                return Results.Ok(characters?.Select(CharacterSummary.Create).ToList() ?? []);
            })
            .WithName("ListCharacters")
            .WithDescription("Lists the connected player's characters.");

        return app;
    }
}

public sealed record CreateCharacterRequest(Guid BohemiaId, string Name);

public sealed record CharacterSummary(
    Guid CharacterId,
    string Name,
    bool SessionActive,
    DateTimeOffset? SessionStartedAt,
    DateTimeOffset? SessionEndedAt)
{
    public static CharacterSummary Create(ApiModels.CharacterDto source) => new(
        source.CharacterId!.Value,
        source.Name!,
        source.SessionActive ?? false,
        source.SessionStartedAt,
        source.SessionEndedAt);
}
