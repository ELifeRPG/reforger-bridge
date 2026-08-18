using ELifeRPG.Bridge.Api.Services;

namespace ELifeRPG.Bridge.Api.Endpoints;

public static class AccountEndpoints
{
    public static WebApplication MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Account");

        group.MapGet("account-info/{bohemiaId:guid}", (
                Guid bohemiaId,
                PlayerSessionTracker sessions) =>
            {
                var resolution = sessions.ResolveAccountId(bohemiaId);
                return resolution.Error ?? Results.Ok(new AccountInfoResponse(resolution.AccountId!.Value));
            })
            .WithName("GetAccountInfo")
            .WithDescription("Resolves the backend AccountId for a connected player's Bohemia ID.");

        return app;
    }
}

public sealed record AccountInfoResponse(Guid AccountId);
