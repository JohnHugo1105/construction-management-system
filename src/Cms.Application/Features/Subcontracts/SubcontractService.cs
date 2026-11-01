using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Application.Features.Suppliers;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Subcontracts;
using Cms.Domain.Features.Suppliers;

namespace Cms.Application.Features.Subcontracts;

/// <summary>
/// Subcontract use cases: draft and award work to a subcontractor, record variations, draft and certify progress claims (certifying posts the
/// claim's work as project cost in the same save, voiding voids that cost in the same save), record what was paid and release retention.
/// Every change loads the whole subcontract, checks the version the person opened, and saves once, so the claim chain, the amounts owed and
/// the project's cost never disagree. Nothing here pays anyone: a payment is a person recording that it was paid.
/// </summary>
public sealed class SubcontractService(ISubcontractRepository subcontracts, ISubcontractReader reader, ISupplierRepository suppliers,
    IProjectRepository projects, ICostEntryRepository costs, IOrganizationRepository organizations, INumberSequence numbers,
    ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public const string SequenceKey = "subcontract";

    public static string ClaimSequenceKey(Guid subcontractId) => $"subcontract-claim:{subcontractId}";

    // ---- Reads ------------------------------------------------------------------------------------------------------------------

    public async Task<PagedResult<SubcontractListItemDto>> ListAsync(SubcontractListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        // A project of another company answers 404, not an empty list.
        if (query.ProjectId is { } projectId) await LoadProjectAsync(projectId, cancellationToken);
        SubcontractStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<SubcontractStatus>(query.Status.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) ||
                int.TryParse(query.Status, out _))
                throw new ValidationFailedException("status", "Choose Draft, Active, Completed or Cancelled.");
            status = parsed;
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (search is { Length: > 100 }) search = search[..100];
        return await reader.ListAsync(new SubcontractListFilter(query.ProjectId, query.SubcontractorId, status, search), page, cancellationToken);
    }

    public Task<SubcontractDto> GetAsync(Guid id, CancellationToken cancellationToken) => ReadAsync(id, cancellationToken);

    /// <summary>What a claim for this much work to date would be, before anything is saved.</summary>
    public async Task<ClaimPreviewDto> PreviewClaimAsync(Guid id, long cumulativeWorkMinor, CancellationToken cancellationToken)
    {
        var subcontract = await LoadAsync(id, cancellationToken);
        var breakdown = subcontract.PreviewClaim(cumulativeWorkMinor);
        return new ClaimPreviewDto(breakdown.CumulativeWorkMinor, breakdown.PreviousCumulativeMinor, breakdown.ThisClaimBaseMinor, breakdown.NetMinor,
            breakdown.VatMinor, breakdown.GrossMinor, breakdown.RetentionMinor, breakdown.WithholdingMinor, breakdown.NetPayableMinor);
    }

    // ---- The subcontract --------------------------------------------------------------------------------------------------------

    public async Task<SubcontractDto> CreateAsync(CreateSubcontractRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await subcontracts.FindByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameCreateAsync(already, request, cancellationToken);

        var supplier = await LoadSupplierAsync(request.SubcontractorId, cancellationToken);
        var project = await LoadProjectAsync(request.ProjectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var subcontract = Subcontract.Create(Guid.NewGuid(), currentUser.TenantId, supplier, project, request.SubProjectId, request.Title, request.Notes,
            request.ContractSumMinor, CostingInput.ParseVatTreatment(request.VatTreatment), organization.VatRateBps,
            request.RetentionBps ?? Subcontract.DefaultRetentionBps, request.WithholdingBps ?? Subcontract.DefaultWithholdingBps, currentUser.UserId,
            requestId, clock.UtcNow);
        // The number is taken only once the details pass their checks, so a refused form leaves no gap.
        subcontract.AssignNumber(Subcontract.FormatNumber(await numbers.NextAsync(SequenceKey, cancellationToken)));

        subcontracts.Add(subcontract);
        audit.Record("Subcontract.Drafted", nameof(Subcontract), subcontract.Id,
            $"Drafted subcontract {subcontract.Number} with {supplier.Name} on {project.Name} for {Money(subcontract.RevisedGrossMinor, organization.CurrencyCode)} with VAT.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form sent twice at once: the database let the first through. Answer with what the first made.
            if (await subcontracts.FindByClientRequestAsync(requestId, cancellationToken) is { } first)
                return await SameCreateAsync(first, request, cancellationToken);
            throw;
        }
        return await ReadAsync(subcontract.Id, cancellationToken);
    }

    public async Task<SubcontractDto> UpdateAsync(Guid id, UpdateSubcontractRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var supplier = await LoadSupplierAsync(request.SubcontractorId, cancellationToken);
        subcontract.UpdateDraft(supplier, project, request.SubProjectId, request.Title, request.Notes, request.ContractSumMinor,
            CostingInput.ParseVatTreatment(request.VatTreatment), request.RetentionBps ?? Subcontract.DefaultRetentionBps,
            request.WithholdingBps ?? Subcontract.DefaultWithholdingBps, clock.UtcNow);
        audit.Record("Subcontract.Updated", nameof(Subcontract), subcontract.Id, $"Updated draft subcontract {subcontract.Number} with {supplier.Name}.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    public async Task<SubcontractDto> AwardAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var supplier = await LoadSupplierAsync(subcontract.SupplierId, cancellationToken);
        subcontract.Award(supplier, project, currentUser.UserId, clock.UtcNow);
        audit.Record("Subcontract.Awarded", nameof(Subcontract), subcontract.Id,
            $"Awarded subcontract {subcontract.Number} to {supplier.Name} on {project.Name}.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    public async Task<SubcontractDto> CompleteAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        subcontract.Complete(currentUser.UserId, clock.UtcNow);
        audit.Record("Subcontract.Completed", nameof(Subcontract), subcontract.Id, $"Marked subcontract {subcontract.Number} completed.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    /// <summary>A draft is cancelled by whoever drafts subcontracts; an awarded one only by an approver.</summary>
    public async Task<SubcontractDto> CancelAsync(Guid id, SubcontractReasonRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken, StatePermission);
        var wasActive = subcontract.Status == SubcontractStatus.Active;
        subcontract.Cancel(request.Reason, currentUser.UserId, clock.UtcNow);
        // The reason is free text and stays on the record, not in the audit line.
        audit.Record("Subcontract.Cancelled", nameof(Subcontract), subcontract.Id,
            $"Cancelled {(wasActive ? "awarded" : "draft")} subcontract {subcontract.Number}.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    /// <summary>A draft's variations are recorded by whoever drafts subcontracts; an awarded subcontract's only by an approver.</summary>
    public async Task<SubcontractDto> AddVariationAsync(Guid id, AddVariationRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken, StatePermission);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var variation = subcontract.AddVariation(Guid.NewGuid(), request.Description, request.AmountMinor, request.VariationDate, currentUser.UserId,
            organization.TodayLocal(clock.UtcNow), clock.UtcNow);
        audit.Record("Subcontract.VariationAdded", nameof(Subcontract), subcontract.Id,
            $"Recorded variation {variation.Number} on subcontract {subcontract.Number}: the revised sum is now {Money(subcontract.RevisedSumMinor, organization.CurrencyCode)}.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    // ---- Claims -----------------------------------------------------------------------------------------------------------------

    public async Task<SubcontractDto> DraftClaimAsync(Guid id, SaveClaimRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await subcontracts.FindClaimByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameChildAsync(already, id, null, cancellationToken);

        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        subcontract.DraftClaim(Guid.NewGuid(), request.ClaimDate, request.PeriodEnd, request.CumulativeWorkMinor, request.Notes, currentUser.UserId,
            requestId, organization.TodayLocal(clock.UtcNow), clock.UtcNow);
        audit.Record("Subcontract.ClaimDrafted", nameof(Subcontract), subcontract.Id, $"Drafted a claim on subcontract {subcontract.Number}.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (requestId is not null && ex is DomainRuleException or ConcurrencyConflictException)
        {
            if (await subcontracts.FindClaimByClientRequestAsync(requestId, cancellationToken) is { } first)
                return await SameChildAsync(first, id, null, cancellationToken);
            throw;
        }
        return await ReadAsync(subcontract.Id, cancellationToken);
    }

    public async Task<SubcontractDto> UpdateClaimAsync(Guid id, Guid claimId, SaveClaimRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        subcontract.UpdateDraftClaim(claimId, request.ClaimDate, request.PeriodEnd, request.CumulativeWorkMinor, request.Notes,
            organization.TodayLocal(clock.UtcNow), clock.UtcNow);
        audit.Record("Subcontract.ClaimUpdated", nameof(Subcontract), subcontract.Id, $"Updated the draft claim on subcontract {subcontract.Number}.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    public async Task<SubcontractDto> DiscardClaimAsync(Guid id, Guid claimId, SubcontractReasonRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        subcontract.DiscardClaim(claimId, request.Reason, currentUser.UserId, clock.UtcNow);
        audit.Record("Subcontract.ClaimDiscarded", nameof(Subcontract), subcontract.Id, $"Discarded the draft claim on subcontract {subcontract.Number}.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    /// <summary>
    /// Certifies a draft claim and posts its work to the project as a cost entry naming the subcontractor, in one save. The posted cost equals
    /// the claim's net, VAT and gross to the centavo; retention is part of the cost because it is still owed.
    /// </summary>
    public async Task<SubcontractDto> CertifyClaimAsync(Guid id, Guid claimId, VersionRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, project) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        subcontract.EnsureCanCertify(claimId);
        project.EnsureCanReceiveRecordsFor(subcontract.SubProjectId);
        var supplier = await LoadSupplierAsync(subcontract.SupplierId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);

        // Checked above, so a claim number is used only by a certification that can go through.
        var number = await numbers.NextAsync(ClaimSequenceKey(subcontract.Id), cancellationToken);
        var now = clock.UtcNow;
        var costId = Guid.NewGuid();
        var claim = subcontract.CertifyClaim(claimId, checked((int)number), costId, currentUser.UserId, now);
        var cost = CostEntry.RecordSystem(costId, currentUser.TenantId, project, subcontract.SubProjectId, CostOrigin.Subcontract, claim.Id,
            CostDescription(subcontract, claim), ItemType.Subcontract, 1m, "l.s", claim.ThisClaimBaseMinor, subcontract.VatTreatment,
            subcontract.VatRateBps, claim.ClaimDate, currentUser.UserId, now, supplier.Name);

        // The rule that matters most: the project is charged exactly what was certified. A mismatch is a defect, so nothing is saved.
        if (cost.NetMinor != claim.NetMinor || cost.VatMinor != claim.VatMinor || cost.GrossMinor != claim.GrossMinor)
            throw new InvalidOperationException("The cost to post doesn't match the certified claim.");

        costs.Add(cost);
        audit.Record("Subcontract.ClaimCertified", nameof(Subcontract), subcontract.Id,
            $"Certified claim {claim.ClaimNumber} on subcontract {subcontract.Number} for {supplier.Name} and posted " +
            $"{Money(claim.GrossMinor, organization.CurrencyCode)} with VAT to {project.Name}.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    /// <summary>Voids the latest certified claim and the cost it posted, with the same reason, in one save.</summary>
    public async Task<SubcontractDto> VoidClaimAsync(Guid id, Guid claimId, SubcontractReasonRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var now = clock.UtcNow;
        var claim = subcontract.VoidClaim(claimId, request.Reason, currentUser.UserId, now);

        var posted = (await costs.ListByOriginAsync(CostOrigin.Subcontract, claim.Id, cancellationToken)).Where(cost => !cost.IsVoided).ToArray();
        // A certified claim always has its cost; without it the claim and the project's cost would disagree, so nothing is saved.
        if (posted.Length == 0) throw new InvalidOperationException("The certified claim has no posted cost to void.");
        foreach (var cost in posted) cost.Void(currentUser.UserId, claim.VoidReason, now);

        audit.Record("Subcontract.ClaimVoided", nameof(Subcontract), subcontract.Id,
            $"Voided claim {claim.ClaimNumber} on subcontract {subcontract.Number}; its project cost was voided with it.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    // ---- Payments ---------------------------------------------------------------------------------------------------------------

    public async Task<SubcontractDto> RecordClaimPaymentAsync(Guid id, Guid claimId, RecordSubcontractPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await subcontracts.FindPaymentByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SamePaymentAsync(already, id, claimId, cancellationToken);

        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var payment = subcontract.RecordClaimPayment(Guid.NewGuid(), claimId, request.AmountMinor, request.PaidOn, ParseMethod(request.Method),
            request.Reference, request.Note, currentUser.UserId, requestId, organization.TodayLocal(clock.UtcNow), clock.UtcNow);
        var claim = subcontract.FindClaim(claimId);
        audit.Record("Subcontract.PaymentRecorded", nameof(Subcontract), subcontract.Id,
            $"Recorded a payment of {Money(payment.AmountMinor, organization.CurrencyCode)} against claim {claim.ClaimNumber} of subcontract {subcontract.Number}.");
        return await SavePaymentAsync(subcontract, requestId, id, claimId, cancellationToken);
    }

    public async Task<SubcontractDto> RecordRetentionReleaseAsync(Guid id, RecordSubcontractPaymentRequest request, CancellationToken cancellationToken)
    {
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await subcontracts.FindPaymentByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SamePaymentAsync(already, id, null, cancellationToken);

        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var payment = subcontract.RecordRetentionRelease(Guid.NewGuid(), request.AmountMinor, request.PaidOn, ParseMethod(request.Method),
            request.Reference, request.Note, currentUser.UserId, requestId, organization.TodayLocal(clock.UtcNow), clock.UtcNow);
        audit.Record("Subcontract.RetentionReleased", nameof(Subcontract), subcontract.Id,
            $"Recorded a retention release of {Money(payment.AmountMinor, organization.CurrencyCode)} on subcontract {subcontract.Number}.");
        return await SavePaymentAsync(subcontract, requestId, id, null, cancellationToken);
    }

    public async Task<SubcontractDto> VoidPaymentAsync(Guid id, Guid paymentId, SubcontractReasonRequest request, CancellationToken cancellationToken)
    {
        var (subcontract, _) = await LoadForChangeAsync(id, request.Version, cancellationToken);
        var payment = subcontract.VoidPayment(paymentId, request.Reason, currentUser.UserId, clock.UtcNow);
        audit.Record("Subcontract.PaymentVoided", nameof(Subcontract), subcontract.Id,
            payment.Kind == SubcontractPaymentKind.RetentionRelease
                ? $"Voided a retention release on subcontract {subcontract.Number}."
                : $"Voided a payment on subcontract {subcontract.Number}.");
        return await SaveAndReadAsync(subcontract, cancellationToken);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------------------

    private async Task<SubcontractDto> SavePaymentAsync(Subcontract subcontract, string? requestId, Guid id, Guid? claimId,
        CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (requestId is not null && ex is DomainRuleException or ConcurrencyConflictException)
        {
            if (await subcontracts.FindPaymentByClientRequestAsync(requestId, cancellationToken) is { } first)
                return await SamePaymentAsync(first, id, claimId, cancellationToken);
            throw;
        }
        return await ReadAsync(subcontract.Id, cancellationToken);
    }

    private async Task<SubcontractDto> SaveAndReadAsync(Subcontract subcontract, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(subcontract.Id, cancellationToken);
    }

    /// <summary>A repeated create answers with the original only when it was for the same firm and project.</summary>
    private async Task<SubcontractDto> SameCreateAsync(RequestOwner owner, CreateSubcontractRequest request, CancellationToken cancellationToken)
    {
        var original = await ReadAsync(owner.SubcontractId, cancellationToken);
        return original.ProjectId == request.ProjectId && original.SubcontractorId == request.SubcontractorId
            ? original
            : throw new DomainRuleException("That request was already used for a different subcontract. Reload the page and try again.");
    }

    /// <summary>A repeated payment answers with the subcontract only when it was the same kind of payment, on the same claim (none for retention).</summary>
    private Task<SubcontractDto> SamePaymentAsync(RequestOwner owner, Guid id, Guid? claimId, CancellationToken cancellationToken) =>
        owner.ClaimId == claimId
            ? SameChildAsync(owner, id, null, cancellationToken)
            : throw new DomainRuleException("That request was already used for something else. Reload the page and try again.");

    /// <summary>A repeated claim or payment answers with the subcontract only when it was made on the same subcontract (and claim).</summary>
    private async Task<SubcontractDto> SameChildAsync(RequestOwner owner, Guid id, Guid? claimId, CancellationToken cancellationToken)
    {
        if (owner.SubcontractId != id || (claimId is not null && owner.ClaimId != claimId))
            throw new DomainRuleException("That request was already used for something else. Reload the page and try again.");
        return await ReadAsync(id, cancellationToken);
    }

    /// <summary>Loads the subcontract and its project for a change: the version must be the one the person opened, and an archived project is read-only.</summary>
    private async Task<(Subcontract Subcontract, Project Project)> LoadForChangeAsync(Guid id, int version, CancellationToken cancellationToken,
        Func<Subcontract, string>? permission = null)
    {
        var subcontract = await LoadAsync(id, cancellationToken);
        // A step whose permission depends on the state is refused as "not allowed" before anything else, never as a stale version.
        if (permission is not null) Require(permission(subcontract));
        ConcurrencyGuard.Ensure(subcontract.Version, version);
        var project = await LoadProjectAsync(subcontract.ProjectId, cancellationToken);
        project.EnsureNotArchived();
        return (subcontract, project);
    }

    /// <summary>Cancelling and variations: a draft's by whoever drafts subcontracts, an awarded one's only by an approver.</summary>
    private static string StatePermission(Subcontract subcontract) =>
        subcontract.Status == SubcontractStatus.Draft ? Permissions.SubcontractsManage : Permissions.SubcontractsApprove;

    private void Require(string permission)
    {
        if (!Permissions.IsAllowed(currentUser.Roles, permission))
            throw new UnauthorizedAccessException("The signed-in person doesn't hold the permission this step needs.");
    }

    private async Task<Subcontract> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await subcontracts.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that subcontract");

    private async Task<Supplier> LoadSupplierAsync(Guid id, CancellationToken cancellationToken) =>
        await suppliers.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that subcontractor");

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");

    private async Task<SubcontractDto> ReadAsync(Guid id, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that subcontract");

    private static SubcontractPaymentMethod ParseMethod(string? text) =>
        !string.IsNullOrWhiteSpace(text) && !int.TryParse(text, out _) &&
        Enum.TryParse<SubcontractPaymentMethod>(text.Trim(), ignoreCase: true, out var method) && Enum.IsDefined(method)
            ? method
            : (SubcontractPaymentMethod)(-1);

    /// <summary>"SC-0001 claim 2: Electrical rough-in, Tower A", at most 200 characters.</summary>
    private static string CostDescription(Subcontract subcontract, SubcontractClaim claim)
    {
        var text = $"{subcontract.Number} claim {claim.ClaimNumber}: {subcontract.Title}";
        return text.Length <= 200 ? text : text[..200];
    }

    private static string Money(long minor, string currencyCode) => CostingInput.MoneyText(minor, currencyCode);
}
