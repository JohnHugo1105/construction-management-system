using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Bonds;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// The bonds register: bonds the company posted to clients and bonds it received from subcontractors and suppliers, each with the date it is
/// valid until. Reading needs bonds.read; recording, editing, extending, releasing, marking as called and voiding need bonds.manage. A bond
/// changes no money figure anywhere: these endpoints post no cost, bill or payment.
/// </summary>
public sealed class BondEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var bonds = api.MapFeature("/bonds", "Bonds");

        bonds.MapGet("", async ([FromQuery] Guid? projectId, [FromQuery] string? direction, [FromQuery] string? kind, [FromQuery] string? status,
                [FromQuery] string? expiry, [AsParameters] PageQuery page, BondService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(new BondListQuery(projectId, direction, kind, status, expiry), page.ToRequest(), ct)))
            .RequirePermission(Permissions.BondsRead);

        // The Active amounts by direction, and how many Active bonds have expired or expire soon.
        bonds.MapGet("/totals", async (BondService service, CancellationToken ct) => Results.Ok(await service.TotalsAsync(ct)))
            .RequirePermission(Permissions.BondsRead);

        bonds.MapGet("/{id:guid}", async (Guid id, BondService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct)))
            .RequirePermission(Permissions.BondsRead);

        bonds.MapPost("", async (CreateBondRequest request, BondService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/bonds/{created.Id}", created);
        }).RequirePermission(Permissions.BondsManage);

        bonds.MapPut("/{id:guid}", async (Guid id, UpdateBondRequest request, BondService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct))).RequirePermission(Permissions.BondsManage);

        bonds.MapPost("/{id:guid}/extend", async (Guid id, ExtendBondRequest request, BondService service, CancellationToken ct) =>
            Results.Ok(await service.ExtendAsync(id, request, ct))).RequirePermission(Permissions.BondsManage);

        bonds.MapPost("/{id:guid}/release", async (Guid id, ReleaseBondRequest request, BondService service, CancellationToken ct) =>
            Results.Ok(await service.ReleaseAsync(id, request, ct))).RequirePermission(Permissions.BondsManage);

        bonds.MapPost("/{id:guid}/call", async (Guid id, CallBondRequest request, BondService service, CancellationToken ct) =>
            Results.Ok(await service.MarkCalledAsync(id, request, ct))).RequirePermission(Permissions.BondsManage);

        bonds.MapPost("/{id:guid}/void", async (Guid id, BondReasonRequest request, BondService service, CancellationToken ct) =>
            Results.Ok(await service.VoidAsync(id, request, ct))).RequirePermission(Permissions.BondsManage);

        // A project's own bonds (not voided, Active first), for its page.
        api.MapFeature("/projects/{projectId:guid}/bonds", "Bonds").MapGet("", async (Guid projectId, BondService service, CancellationToken ct) =>
            Results.Ok(await service.ListForProjectAsync(projectId, ct))).RequirePermission(Permissions.BondsRead);
    }
}
