using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.SiteDiary;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// A project's site diary: one structured report per day. Reading needs diary.read; writing, correcting and voiding need diary.write, and the
/// domain then lets only the author (until the end of the next day) or a person who manages projects change a report. A report carries no
/// money: the workers with attendance that day and the deliveries received that day are read beside it, never an amount.
/// </summary>
public sealed class SiteDiaryEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var diary = api.MapFeature("/projects/{projectId:guid}/diary", "Site diary");

        // Newest first.
        diary.MapGet("", async (Guid projectId, [AsParameters] PageQuery page, SiteDiaryService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(projectId, page.ToRequest(), ct))).RequirePermission(Permissions.DiaryRead);

        diary.MapGet("/{date}", async (Guid projectId, DateOnly date, SiteDiaryService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(projectId, date, ct))).RequirePermission(Permissions.DiaryRead);

        // Who was on site and what was delivered on a day, for the form that writes its report.
        diary.MapGet("/{date}/filled-in", async (Guid projectId, DateOnly date, SiteDiaryService service, CancellationToken ct) =>
            Results.Ok(await service.FilledInAsync(projectId, date, ct))).RequirePermission(Permissions.DiaryRead);

        diary.MapPost("", async (Guid projectId, CreateDiaryEntryRequest request, SiteDiaryService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(projectId, request, ct);
            return Results.Created($"/api/projects/{projectId}/diary/{created.ReportDate:yyyy-MM-dd}", created);
        }).RequirePermission(Permissions.DiaryWrite);

        diary.MapPut("/{date}", async (Guid projectId, DateOnly date, UpdateDiaryEntryRequest request, SiteDiaryService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(projectId, date, request, ct))).RequirePermission(Permissions.DiaryWrite);

        diary.MapPost("/{date}/void", async (Guid projectId, DateOnly date, VoidDiaryEntryRequest request, SiteDiaryService service, CancellationToken ct) =>
            Results.Ok(await service.VoidAsync(projectId, date, request, ct))).RequirePermission(Permissions.DiaryWrite);
    }
}
