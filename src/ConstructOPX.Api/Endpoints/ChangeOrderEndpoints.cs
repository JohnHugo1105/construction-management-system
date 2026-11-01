using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.ChangeOrders;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>The budget baseline of a project, and its change orders (docs/api/change-orders.md).</summary>
public sealed class ChangeOrderEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var projects = api.MapFeature("/projects/{projectId:guid}", "Change orders");

        projects.MapGet("/budget-baseline", async (Guid projectId, BudgetBaselineService baseline, CancellationToken ct) =>
            Results.Ok(await baseline.GetAsync(projectId, ct))).RequirePermission(Permissions.ChangeOrdersRead);

        projects.MapPost("/budget-baseline/lock", async (Guid projectId, VersionRequest request, BudgetBaselineService baseline, CancellationToken ct) =>
            Results.Ok(await baseline.LockAsync(projectId, request, ct))).RequirePermission(Permissions.ChangeOrdersApprove);

        projects.MapPost("/budget-baseline/unlock", async (Guid projectId, UnlockBudgetRequest request, BudgetBaselineService baseline,
                CancellationToken ct) =>
            Results.Ok(await baseline.UnlockAsync(projectId, request, ct))).RequirePermission(Permissions.ChangeOrdersApprove);

        projects.MapGet("/change-orders", async (Guid projectId, [FromQuery] string? status, [AsParameters] PageQuery page, ChangeOrderService orders,
                CancellationToken ct) =>
            Results.Ok(await orders.ListAsync(projectId, status, page.ToRequest(), ct))).RequirePermission(Permissions.ChangeOrdersRead);

        projects.MapPost("/change-orders", async (Guid projectId, CreateChangeOrderRequest request, ChangeOrderService orders, CancellationToken ct) =>
        {
            var created = await orders.CreateAsync(projectId, request, ct);
            return Results.Created($"/api/change-orders/{created.Id}", created);
        }).RequirePermission(Permissions.ChangeOrdersManage);

        var group = api.MapFeature("/change-orders", "Change orders");

        group.MapGet("/{id:guid}", async (Guid id, ChangeOrderService orders, CancellationToken ct) => Results.Ok(await orders.GetAsync(id, ct)))
            .RequirePermission(Permissions.ChangeOrdersRead);

        group.MapPut("/{id:guid}", async (Guid id, UpdateChangeOrderRequest request, ChangeOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.UpdateAsync(id, request, ct))).RequirePermission(Permissions.ChangeOrdersManage);

        group.MapPost("/{id:guid}/lines", async (Guid id, SaveChangeOrderLineRequest request, ChangeOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.AddLineAsync(id, request, ct))).RequirePermission(Permissions.ChangeOrdersManage);

        group.MapPut("/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, SaveChangeOrderLineRequest request, ChangeOrderService orders,
                CancellationToken ct) =>
            Results.Ok(await orders.UpdateLineAsync(id, lineId, request, ct))).RequirePermission(Permissions.ChangeOrdersManage);

        group.MapPost("/{id:guid}/lines/{lineId:guid}/remove", async (Guid id, Guid lineId, VersionRequest request, ChangeOrderService orders,
                CancellationToken ct) =>
            Results.Ok(await orders.RemoveLineAsync(id, lineId, request, ct))).RequirePermission(Permissions.ChangeOrdersManage);

        group.MapPost("/{id:guid}/submit", async (Guid id, VersionRequest request, ChangeOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.SubmitAsync(id, request, ct))).RequirePermission(Permissions.ChangeOrdersManage);

        group.MapPost("/{id:guid}/discard", async (Guid id, ChangeOrderReasonRequest request, ChangeOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.DiscardAsync(id, request, ct))).RequirePermission(Permissions.ChangeOrdersManage);

        group.MapPost("/{id:guid}/approve", async (Guid id, ApproveChangeOrderRequest request, ChangeOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.ApproveAsync(id, request, ct))).RequirePermission(Permissions.ChangeOrdersApprove);

        group.MapPost("/{id:guid}/reject", async (Guid id, ChangeOrderReasonRequest request, ChangeOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.RejectAsync(id, request, ct))).RequirePermission(Permissions.ChangeOrdersApprove);

        group.MapPost("/{id:guid}/void", async (Guid id, ChangeOrderReasonRequest request, ChangeOrderService orders, CancellationToken ct) =>
            Results.Ok(await orders.VoidAsync(id, request, ct))).RequirePermission(Permissions.ChangeOrdersApprove);
    }
}
