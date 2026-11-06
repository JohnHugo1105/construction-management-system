using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Subcontracts;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// Subcontracts, their progress claims and payments. Every write answers with the whole subcontract. Drafting (the subcontract and its claims)
/// needs <c>subcontracts.manage</c>; awarding, certifying, voiding, completing and recording payments need <c>subcontracts.approve</c>.
/// Cancelling and recording a variation depend on the state: a draft's by a drafter, an awarded one's by an approver; the endpoint admits
/// readers and the service checks the permission the state needs (a refusal is 403).
/// </summary>
public sealed class SubcontractEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/subcontracts", "Subcontracts");

        group.MapGet("", async ([FromQuery] Guid? projectId, [FromQuery] Guid? subcontractorId, [FromQuery] string? status, [FromQuery] string? search,
                [AsParameters] PageQuery page, SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.ListAsync(new SubcontractListQuery(projectId, subcontractorId, status, search), page.ToRequest(), ct)))
            .RequirePermission(Permissions.SubcontractsRead);

        group.MapGet("/{id:guid}", async (Guid id, SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.GetAsync(id, ct))).RequirePermission(Permissions.SubcontractsRead);

        group.MapPost("", async (CreateSubcontractRequest request, SubcontractService subcontracts, CancellationToken ct) =>
        {
            var created = await subcontracts.CreateAsync(request, ct);
            return Results.Created($"/api/subcontracts/{created.Id}", created);
        }).RequirePermission(Permissions.SubcontractsManage);

        group.MapPut("/{id:guid}", async (Guid id, UpdateSubcontractRequest request, SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.UpdateAsync(id, request, ct))).RequirePermission(Permissions.SubcontractsManage);

        group.MapPost("/{id:guid}/award", async (Guid id, VersionRequest request, SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.AwardAsync(id, request, ct))).RequirePermission(Permissions.SubcontractsApprove);

        group.MapPost("/{id:guid}/complete", async (Guid id, VersionRequest request, SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.CompleteAsync(id, request, ct))).RequirePermission(Permissions.SubcontractsApprove);

        // Draft: subcontracts.manage; awarded: subcontracts.approve (checked by the service).
        group.MapPost("/{id:guid}/cancel", async (Guid id, SubcontractReasonRequest request, SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.CancelAsync(id, request, ct))).RequirePermission(Permissions.SubcontractsRead);

        // Draft: subcontracts.manage; awarded: subcontracts.approve (checked by the service).
        group.MapPost("/{id:guid}/variations", async (Guid id, AddVariationRequest request, SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.AddVariationAsync(id, request, ct))).RequirePermission(Permissions.SubcontractsRead);

        // ---- Claims
        group.MapGet("/{id:guid}/claims/preview", async (Guid id, [FromQuery] long cumulativeWorkMinor, SubcontractService subcontracts,
                CancellationToken ct) =>
            Results.Ok(await subcontracts.PreviewClaimAsync(id, cumulativeWorkMinor, ct))).RequirePermission(Permissions.SubcontractsManage);

        group.MapPost("/{id:guid}/claims", async (Guid id, SaveClaimRequest request, SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.DraftClaimAsync(id, request, ct))).RequirePermission(Permissions.SubcontractsManage);

        group.MapPut("/{id:guid}/claims/{claimId:guid}", async (Guid id, Guid claimId, SaveClaimRequest request, SubcontractService subcontracts,
                CancellationToken ct) =>
            Results.Ok(await subcontracts.UpdateClaimAsync(id, claimId, request, ct))).RequirePermission(Permissions.SubcontractsManage);

        group.MapPost("/{id:guid}/claims/{claimId:guid}/discard", async (Guid id, Guid claimId, SubcontractReasonRequest request,
                SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.DiscardClaimAsync(id, claimId, request, ct))).RequirePermission(Permissions.SubcontractsManage);

        group.MapPost("/{id:guid}/claims/{claimId:guid}/certify", async (Guid id, Guid claimId, VersionRequest request, SubcontractService subcontracts,
                CancellationToken ct) =>
            Results.Ok(await subcontracts.CertifyClaimAsync(id, claimId, request, ct))).RequirePermission(Permissions.SubcontractsApprove);

        group.MapPost("/{id:guid}/claims/{claimId:guid}/void", async (Guid id, Guid claimId, SubcontractReasonRequest request,
                SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.VoidClaimAsync(id, claimId, request, ct))).RequirePermission(Permissions.SubcontractsApprove);

        // ---- Payments: a person recording what was paid; nothing here pays anyone.
        group.MapPost("/{id:guid}/claims/{claimId:guid}/payments", async (Guid id, Guid claimId, RecordSubcontractPaymentRequest request,
                SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.RecordClaimPaymentAsync(id, claimId, request, ct))).RequirePermission(Permissions.SubcontractsApprove);

        group.MapPost("/{id:guid}/retention-releases", async (Guid id, RecordSubcontractPaymentRequest request, SubcontractService subcontracts,
                CancellationToken ct) =>
            Results.Ok(await subcontracts.RecordRetentionReleaseAsync(id, request, ct))).RequirePermission(Permissions.SubcontractsApprove);

        group.MapPost("/{id:guid}/payments/{paymentId:guid}/void", async (Guid id, Guid paymentId, SubcontractReasonRequest request,
                SubcontractService subcontracts, CancellationToken ct) =>
            Results.Ok(await subcontracts.VoidPaymentAsync(id, paymentId, request, ct))).RequirePermission(Permissions.SubcontractsApprove);
    }
}
