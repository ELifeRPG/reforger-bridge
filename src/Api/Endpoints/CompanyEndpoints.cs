using ELifeRPG.Bridge.Api.Extensions;
using ELifeRPG.BackendApiClient;
using ApiModels = ELifeRPG.BackendApiClient.Models;

namespace ELifeRPG.Bridge.Api.Endpoints;

public static class CompanyEndpoints
{
    public static WebApplication MapCompanyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Companies");

        group.MapPost("companies", async (
                CreateCompanyRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                ApiModels.CompanyDto? company;
                try
                {
                    company = await apiClient.Api.Companies.PostAsync(
                        new ApiModels.CreateCompanyRequestDto { Name = request.Name, FounderCharacterId = request.FounderCharacterId },
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return company is null
                    ? Results.Problem("Central API returned an empty company response.")
                    : Results.Ok(CompanySummary.Create(company));
            })
            .WithName("CreateCompany")
            .WithDescription("Creates a new company; the founder becomes its first member.");

        group.MapGet("companies", async (EliferpgApiClient apiClient, CancellationToken cancellationToken) =>
            {
                var companies = await apiClient.Api.Companies.GetAsync(cancellationToken: cancellationToken);
                return Results.Ok(companies?.Select(CompanySummary.Create).ToList() ?? []);
            })
            .WithName("ListCompanies")
            .WithDescription("Lists companies.");

        group.MapGet("companies/{companyId:guid}", async (
                Guid companyId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                ApiModels.CompanyDetailsDto? company;
                try
                {
                    company = await apiClient.Api.Companies[companyId].GetAsync(cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return company is null
                    ? Results.NotFound()
                    : Results.Ok(CompanyDetails.Create(company));
            })
            .WithName("GetCompany")
            .WithDescription("Gets company details, including its members.");

        group.MapPost("companies/{companyId:guid}/members", async (
                Guid companyId,
                AddCompanyMemberRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    await apiClient.Api.Companies[companyId].Members.PostAsync(
                        new ApiModels.AddMemberRequestDto { CharacterId = request.CharacterId },
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return Results.Ok();
            })
            .WithName("AddCompanyMember")
            .WithDescription("Adds a character as a member of a company.");

        group.MapPost("companies/{companyId:guid}/applications", async (
                Guid companyId,
                SubmitCompanyApplicationRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                ApiModels.CompanyApplicationDto? application;
                try
                {
                    application = await apiClient.Api.Companies[companyId].Applications.PostAsync(
                        new ApiModels.SubmitApplicationRequestDto { CharacterId = request.CharacterId, Message = request.Message },
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return application is null
                    ? Results.Problem("Central API returned an empty application response.")
                    : Results.Ok(CompanyApplicationSummary.Create(application));
            })
            .WithName("SubmitCompanyApplication")
            .WithDescription("Submits a character's application to join a company.");

        group.MapGet("companies/{companyId:guid}/applications", async (
                Guid companyId,
                Guid actingCharacterId,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                List<ApiModels.CompanyApplicationDto>? applications;
                try
                {
                    applications = await apiClient.Api.Companies[companyId].Applications.GetAsync(
                        config => config.QueryParameters.ActingCharacterId = actingCharacterId,
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return Results.Ok(applications?.Select(CompanyApplicationSummary.Create).ToList() ?? []);
            })
            .WithName("ListCompanyApplications")
            .WithDescription("Lists a company's applications. Requires ManageMembers permission in the company.");

        group.MapPut("companies/{companyId:guid}/applications/{applicationId:guid}/confirm", async (
                Guid companyId,
                Guid applicationId,
                ActingCharacterRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    await apiClient.Api.Companies[companyId].Applications[applicationId].Confirm.PutAsync(
                        new ApiModels.ActingCharacterRequestDto { ActingCharacterId = request.ActingCharacterId },
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return Results.Ok();
            })
            .WithName("ConfirmCompanyApplication")
            .WithDescription("Marks a pending application as InProgress. Requires ManageMembers permission in the company.");

        group.MapPut("companies/{companyId:guid}/applications/{applicationId:guid}/accept", async (
                Guid companyId,
                Guid applicationId,
                ActingCharacterRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    await apiClient.Api.Companies[companyId].Applications[applicationId].Accept.PutAsync(
                        new ApiModels.ActingCharacterRequestDto { ActingCharacterId = request.ActingCharacterId },
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return Results.Ok();
            })
            .WithName("AcceptCompanyApplication")
            .WithDescription("Accepts an application, adding the character as a member in the company's default position. Requires ManageMembers permission in the company.");

        group.MapPut("companies/{companyId:guid}/applications/{applicationId:guid}/deny", async (
                Guid companyId,
                Guid applicationId,
                ActingCharacterRequest request,
                EliferpgApiClient apiClient,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    await apiClient.Api.Companies[companyId].Applications[applicationId].Deny.PutAsync(
                        new ApiModels.ActingCharacterRequestDto { ActingCharacterId = request.ActingCharacterId },
                        cancellationToken: cancellationToken);
                }
                catch (ApiModels.ProblemDetails problem)
                {
                    return Results.Problem(title: problem.Title, detail: problem.Detail, statusCode: problem.ResponseStatusCode);
                }

                return Results.Ok();
            })
            .WithName("DenyCompanyApplication")
            .WithDescription("Denies an application. Requires ManageMembers permission in the company.");

        return app;
    }
}

public sealed record CompanySummary(Guid CompanyId, string Name, int MemberCount)
{
    public static CompanySummary Create(ApiModels.CompanyDto source) => new(
        source.CompanyId!.Value,
        source.Name!,
        source.MemberCount!.ToInt32());
}

public sealed record CompanyMember(Guid CharacterId, string PositionName)
{
    public static CompanyMember Create(ApiModels.CompanyMembershipDto source) => new(
        source.CharacterId!.Value,
        source.PositionName!);
}

public sealed record CompanyDetails(Guid CompanyId, string Name, List<CompanyMember> Members)
{
    public static CompanyDetails Create(ApiModels.CompanyDetailsDto source) => new(
        source.CompanyId!.Value,
        source.Name!,
        source.Members?.Select(CompanyMember.Create).ToList() ?? []);
}

public sealed record CompanyApplicationSummary(Guid ApplicationId, Guid CharacterId, string Message, string Status)
{
    public static CompanyApplicationSummary Create(ApiModels.CompanyApplicationDto source) => new(
        source.ApplicationId!.Value,
        source.CharacterId!.Value,
        source.Message!,
        source.Status!);
}

public sealed record CreateCompanyRequest(string Name, Guid FounderCharacterId);

public sealed record AddCompanyMemberRequest(Guid CharacterId);

public sealed record SubmitCompanyApplicationRequest(Guid CharacterId, string Message);

public sealed record ActingCharacterRequest(Guid ActingCharacterId);
