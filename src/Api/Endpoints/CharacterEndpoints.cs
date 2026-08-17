using ELifeRPG.BackendApiClient;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Endpoints;

public static class CharacterEndpoints
{
    public static WebApplication MapCharacterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Character");

        group.MapPost("characters", async (
                ApiModels.CreateCharacterRequestDto request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                ApiModels.CharacterDto? character;
                try
                {
                    character = await apiClient.Api.Characters.PostAsync(request, cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return character is null
                    ? Results.Problem("Central API returned an empty character response.")
                    : Results.Ok(CharacterSummary.Create(character));
            })
            .WithName("CreateCharacter")
            .WithDescription("Creates a new character for an account.");

        group.MapGet("accounts/{accountId:guid}/characters", async (
                Guid accountId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                var characters = await apiClient.Api.Accounts[accountId].Characters.GetAsync(cancellationToken: cancellationToken);
                return Results.Ok(characters?.Select(CharacterSummary.Create).ToList() ?? []);
            })
            .WithName("ListAccountCharacters")
            .WithDescription("Lists characters for an account.");

        return app;
    }
}

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
