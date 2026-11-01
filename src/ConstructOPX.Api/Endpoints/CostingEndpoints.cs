using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Costing;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>Budget lines: the planned cost of a project. Reading needs costs.read, changing needs costs.write.</summary>
public sealed class BudgetLineEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var inProject = api.MapFeature("/projects/{projectId:guid}/budget-lines", "Budget lines");

        inProject.MapGet("", async (Guid projectId, [FromQuery] Guid? subProjectId, [FromQuery] int? division, [FromQuery] string? itemType,
                [FromQuery] string? search, [FromQuery] bool? includeRemoved, [AsParameters] PageQuery page, BudgetLineService lines, CancellationToken ct) =>
            Results.Ok(await lines.ListAsync(projectId, new BudgetLineListQuery(subProjectId, division, itemType, search, includeRemoved ?? false),
                page.ToRequest(), ct)))
            .RequirePermission(Permissions.CostsRead);

        inProject.MapGet("/export", async (Guid projectId, [FromQuery] Guid? subProjectId, [FromQuery] int? division, [FromQuery] string? itemType,
                [FromQuery] string? search, [FromQuery] bool? includeRemoved, BudgetLineService lines, CancellationToken ct) =>
            {
                var file = await lines.ExportAsync(projectId, new BudgetLineListQuery(subProjectId, division, itemType, search, includeRemoved ?? false), ct);
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .RequirePermission(Permissions.CostsRead);

        inProject.MapPost("", async (Guid projectId, CreateBudgetLineRequest request, BudgetLineService lines, CancellationToken ct) =>
        {
            var created = await lines.CreateAsync(projectId, request, ct);
            return Results.Created($"/api/budget-lines/{created.Id}", created);
        }).RequirePermission(Permissions.CostsWrite);

        var line = api.MapFeature("/budget-lines", "Budget lines");

        line.MapGet("/{id:guid}", async (Guid id, BudgetLineService lines, CancellationToken ct) => Results.Ok(await lines.GetAsync(id, ct)))
            .RequirePermission(Permissions.CostsRead);

        line.MapPut("/{id:guid}", async (Guid id, UpdateBudgetLineRequest request, BudgetLineService lines, CancellationToken ct) =>
            Results.Ok(await lines.UpdateAsync(id, request, ct))).RequirePermission(Permissions.CostsWrite);

        line.MapPost("/{id:guid}/remove", async (Guid id, RemoveBudgetLineRequest request, BudgetLineService lines, CancellationToken ct) =>
            Results.Ok(await lines.RemoveAsync(id, request, ct))).RequirePermission(Permissions.CostsWrite);
    }
}

/// <summary>Cost entries: what a project has actually spent. Reading needs costs.read, changing needs costs.write.</summary>
public sealed class CostEntryEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var inProject = api.MapFeature("/projects/{projectId:guid}/cost-entries", "Cost entries");

        inProject.MapGet("", async (Guid projectId, [FromQuery] Guid? subProjectId, [FromQuery] int? division, [FromQuery] string? itemType,
                [FromQuery] string? origin, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
                [FromQuery] bool? includeVoided, [AsParameters] PageQuery page, CostEntryService entries, CancellationToken ct) =>
            Results.Ok(await entries.ListAsync(projectId,
                new CostEntryListQuery(subProjectId, division, itemType, origin, from, to, search, includeVoided ?? false), page.ToRequest(), ct)))
            .RequirePermission(Permissions.CostsRead);

        inProject.MapGet("/export", async (Guid projectId, [FromQuery] Guid? subProjectId, [FromQuery] int? division, [FromQuery] string? itemType,
                [FromQuery] string? origin, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
                [FromQuery] bool? includeVoided, CostEntryService entries, CancellationToken ct) =>
            {
                var file = await entries.ExportAsync(projectId,
                    new CostEntryListQuery(subProjectId, division, itemType, origin, from, to, search, includeVoided ?? false), ct);
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .RequirePermission(Permissions.CostsRead);

        inProject.MapPost("", async (Guid projectId, CreateCostEntryRequest request, CostEntryService entries, CancellationToken ct) =>
        {
            var created = await entries.CreateAsync(projectId, request, ct);
            return Results.Created($"/api/cost-entries/{created.Id}", created);
        }).RequirePermission(Permissions.CostsWrite);

        var entry = api.MapFeature("/cost-entries", "Cost entries");

        entry.MapGet("/{id:guid}", async (Guid id, CostEntryService entries, CancellationToken ct) => Results.Ok(await entries.GetAsync(id, ct)))
            .RequirePermission(Permissions.CostsRead);

        entry.MapPut("/{id:guid}", async (Guid id, UpdateCostEntryRequest request, CostEntryService entries, CancellationToken ct) =>
            Results.Ok(await entries.UpdateAsync(id, request, ct))).RequirePermission(Permissions.CostsWrite);

        entry.MapPost("/{id:guid}/void", async (Guid id, VoidCostEntryRequest request, CostEntryService entries, CancellationToken ct) =>
            Results.Ok(await entries.VoidAsync(id, request, ct))).RequirePermission(Permissions.CostsWrite);
    }
}

/// <summary>The choices the budget line and cost entry forms offer. Needs costs.read, the same as seeing the lines themselves.</summary>
public sealed class CostingReferenceEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api) =>
        api.MapFeature("/costing", "Costing").MapGet("/reference", async (CostingReferenceService reference, CancellationToken ct) =>
            Results.Ok(await reference.GetAsync(ct))).RequirePermission(Permissions.CostsRead);
}
