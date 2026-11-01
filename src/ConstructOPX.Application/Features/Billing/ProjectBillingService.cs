using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Billing;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Application.Features.Billing;

/// <summary>
/// A project's billing position and terms, the preview of a bill before it is saved, and the company's receivables. Every figure is
/// summed in SQL from the issued bills and their payments, so the project tab, the receivables overview and a bill agree.
/// </summary>
public sealed class ProjectBillingService(IBillingReader reader, IProjectBillingTermsRepository termsRepository, IClientInvoiceRepository invoices,
    IProjectRepository projects, IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public async Task<ProjectBillingDto> GetAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        var today = (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);
        return await reader.GetProjectBillingAsync(projectId, today, page, cancellationToken) ?? throw new NotFoundException("that project");
    }

    public async Task<ReceivablesDto> GetReceivablesAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var today = (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);
        return await reader.GetReceivablesAsync(today, page, cancellationToken);
    }

    /// <summary>
    /// Saves the project's billing terms (created the first time). They apply to bills prepared from now on; a bill keeps the terms it
    /// was made with.
    /// </summary>
    public async Task<BillingTermsDto> SaveTermsAsync(Guid projectId, SaveBillingTermsRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadProjectAsync(projectId, cancellationToken);
        var now = clock.UtcNow;
        var terms = await termsRepository.GetByProjectAsync(projectId, cancellationToken);
        if (terms is null)
        {
            // Nobody saved terms yet, so the person saw the defaults (version 0).
            ConcurrencyGuard.Ensure(0, request.Version);
            terms = ProjectBillingTerms.Create(Guid.NewGuid(), currentUser.TenantId, project, request.RetentionBps, request.CreditableWithholdingBps,
                request.PaymentTermsDays, now, request.AdvanceBps ?? ProjectBillingTerms.DefaultAdvanceBps,
                request.VatWithholdingBps ?? ProjectBillingTerms.DefaultVatWithholdingBps, request.IsGovernmentClient ?? false);
            termsRepository.Add(terms);
        }
        else
        {
            ConcurrencyGuard.Ensure(terms.Version, request.Version);
            terms.Change(project, request.RetentionBps, request.CreditableWithholdingBps, request.PaymentTermsDays, now, request.AdvanceBps,
                request.VatWithholdingBps, request.IsGovernmentClient);
        }

        audit.Record("Billing.TermsChanged", nameof(ProjectBillingTerms), terms.Id,
            $"Changed the billing terms of {project.Name}: retention {Percent(terms.RetentionBps)}, withholding {Percent(terms.CreditableWithholdingBps)}, " +
            $"VAT withholding {Percent(terms.VatWithholdingBps)}, down payment {Percent(terms.AdvanceBps)}, " +
            $"payment due {terms.PaymentTermsDays} day{(terms.PaymentTermsDays == 1 ? "" : "s")} after billing" +
            (terms.IsGovernmentClient ? ", government client." : "."));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(terms);
    }

    internal static BillingTermsDto ToDto(ProjectBillingTerms terms) => new(terms.RetentionBps, terms.CreditableWithholdingBps, terms.PaymentTermsDays,
        terms.AdvanceBps, terms.VatWithholdingBps, terms.IsGovernmentClient, false, terms.Version);

    /// <summary>
    /// What a progress bill for this much work completed to date would come to, without saving anything. It is worked out by preparing
    /// the same draft a save would, so the preview and the saved bill can't disagree, and the same refusals apply.
    /// </summary>
    public async Task<BillPreviewDto> PreviewAsync(Guid projectId, long cumulativeWorkMinor, DateOnly? billingDate, CancellationToken cancellationToken)
    {
        var project = await LoadProjectAsync(projectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var ledger = await invoices.GetLedgerAsync(projectId, cancellationToken);
        var terms = (await termsRepository.GetByProjectAsync(projectId, cancellationToken))?.Snapshot ?? BillingTermsSnapshot.Defaults;

        var draft = ClientInvoice.DraftProgress(Guid.NewGuid(), currentUser.TenantId, project, billingDate ?? today, "Preview", cumulativeWorkMinor, ledger,
            terms, organization.VatRateBps, currentUser.UserId, null, today, clock.UtcNow);
        return new BillPreviewDto(project.ContractPriceMinor!.Value, draft.PreviousCumulativeWorkMinor, draft.CumulativeWorkMinor!.Value, draft.GrossMinor,
            draft.NetMinor, draft.VatMinor, draft.VatRateBps, draft.RetentionBps, draft.RetentionMinor, draft.WithholdingBps, draft.WithholdingMinor,
            draft.VatWithholdingBps, draft.VatWithholdingMinor, draft.AdvanceRecoveryMinor, draft.NetReceivableMinor, draft.PaymentTermsDays, draft.DueDate);
    }

    private async Task<Project> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await projects.GetAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project");

    internal static string Percent(int bps) => (bps / 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
