using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Domain.Common;
using Cms.Domain.Features.Billing;
using Cms.Domain.Features.Projects;

namespace Cms.Application.Features.Billing;

/// <summary>
/// Bills to the client and the payments recorded against them. A bill is prepared as a draft (billing.manage), issued by an approver
/// (billing.approve), which numbers and locks it, and voided with a reason. Each step is one save. Recording a payment is a person
/// saying the client paid: nothing here collects money or sends anything to a client. Amounts never appear in an audit line.
/// </summary>
public sealed class ClientInvoiceService(IClientInvoiceRepository invoices, IBillingReader reader, IProjectBillingTermsRepository termsRepository,
    IProjectRepository projects, IOrganizationRepository organizations, INumberSequence numbers, ICurrentUser currentUser, IClock clock,
    IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public const string NumberSequenceKey = "client-invoice";

    public async Task<PagedResult<ClientInvoiceListItemDto>> ListAsync(ClientInvoiceListQuery query, PageRequest page, CancellationToken cancellationToken) =>
        await reader.ListAsync(query, await TodayAsync(cancellationToken), page, cancellationToken);

    public async Task<ClientInvoiceDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ReadAsync(id, await TodayAsync(cancellationToken), cancellationToken);

    /// <summary>Prepares a draft bill. A repeated request id answers with the draft the first submission made.</summary>
    public async Task<ClientInvoiceDto> CreateAsync(Guid projectId, CreateClientInvoiceRequest request, CancellationToken cancellationToken)
    {
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null && await invoices.FindIdByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SameDraftAsync(already, projectId, request, cancellationToken);

        try
        {
            return await DraftAsync(projectId, request, requestId, cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form sent twice at once: the database lets only the first draft with this request id in. Answer with it.
            if (await invoices.FindIdByClientRequestAsync(requestId, cancellationToken) is { } original)
                return await SameDraftAsync(original, projectId, request, cancellationToken);
            throw;
        }
    }

    private async Task<ClientInvoiceDto> DraftAsync(Guid projectId, CreateClientInvoiceRequest request, string? requestId, CancellationToken cancellationToken)
    {
        var kind = ParseKind(request.Kind);
        var project = await LoadProjectAsync(projectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var now = clock.UtcNow;
        var ledger = await invoices.GetLedgerAsync(projectId, cancellationToken);
        var terms = await TermsAsync(projectId, cancellationToken);

        var invoice = kind switch
        {
            ClientInvoiceKind.Progress => ClientInvoice.DraftProgress(Guid.NewGuid(), currentUser.TenantId, project, request.BillingDate, request.Description,
                Required(request.CumulativeWorkMinor, "cumulativeWorkMinor", "Enter the value of the work completed to date."), ledger, terms,
                organization.VatRateBps, currentUser.UserId, requestId, today, now),
            ClientInvoiceKind.Advance => ClientInvoice.DraftAdvance(Guid.NewGuid(), currentUser.TenantId, project, request.BillingDate, request.Description,
                ledger, await invoices.AnyLiveAdvanceAsync(projectId, cancellationToken), terms, currentUser.UserId, requestId, today, now),
            _ => ClientInvoice.DraftRetentionRelease(Guid.NewGuid(), currentUser.TenantId, project, request.BillingDate, request.Description,
                Required(request.AmountMinor, "amountMinor", "Enter the amount of retention to release."), ledger, terms, currentUser.UserId, requestId, today, now)
        };

        invoices.Add(invoice);
        audit.Record("Billing.Drafted", nameof(ClientInvoice), invoice.Id, $"Prepared a draft {KindWords(invoice.Kind)} for {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(invoice.Id, today, cancellationToken);
    }

    /// <summary>A repeated request id answers with the original draft only when it was for the same project and kind of bill.</summary>
    private async Task<ClientInvoiceDto> SameDraftAsync(Guid originalId, Guid projectId, CreateClientInvoiceRequest request, CancellationToken cancellationToken)
    {
        var original = await ReadAsync(originalId, await TodayAsync(cancellationToken), cancellationToken);
        return original.ProjectId == projectId && original.Kind == ParseKind(request.Kind).ToString()
            ? original
            : throw new DomainRuleException("That request id was already used for a different bill. Reload the page and try again.");
    }

    /// <summary>Saves a draft again with new figures. It also brings a draft that another bill made out of date back in line.</summary>
    public async Task<ClientInvoiceDto> UpdateAsync(Guid id, UpdateClientInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(invoice.Version, request.Version);
        var project = await LoadProjectAsync(invoice.ProjectId, cancellationToken);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var ledger = await invoices.GetLedgerAsync(invoice.ProjectId, cancellationToken);
        var terms = await TermsAsync(invoice.ProjectId, cancellationToken);

        // A down payment is worked out from the contract price and the terms; it takes no amount.
        var amount = invoice.Kind switch
        {
            ClientInvoiceKind.Progress => Required(request.CumulativeWorkMinor, "cumulativeWorkMinor", "Enter the value of the work completed to date."),
            ClientInvoiceKind.Advance => 0,
            _ => Required(request.AmountMinor, "amountMinor", "Enter the amount of retention to release.")
        };
        invoice.UpdateDraft(project, request.BillingDate, request.Description, amount, ledger, terms, organization.VatRateBps, today, clock.UtcNow);

        audit.Record("Billing.DraftChanged", nameof(ClientInvoice), invoice.Id, $"Changed a draft {KindWords(invoice.Kind)} for {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(invoice.Id, today, cancellationToken);
    }

    /// <summary>
    /// Issues a draft: checks again that its figures follow the project's latest issued bill (another bill may have been issued or voided
    /// since it was prepared) and, for a down payment, the contract price and terms now; takes the next bill number and locks it. Two
    /// issues at the same moment can't both follow the same bill.
    /// </summary>
    public async Task<ClientInvoiceDto> IssueAsync(Guid id, VersionRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(invoice.Version, request.Version);
        var project = await LoadProjectAsync(invoice.ProjectId, cancellationToken);
        var head = await invoices.GetChainHeadAsync(invoice.ProjectId, cancellationToken);
        var ledger = await invoices.GetLedgerAsync(invoice.ProjectId, cancellationToken);
        var terms = await TermsAsync(invoice.ProjectId, cancellationToken);

        // Check before taking a number, so a refusal doesn't use one up.
        invoice.EnsureCanIssue(project, ledger, terms);
        var sequence = await numbers.NextAsync(NumberSequenceKey, cancellationToken);
        var now = clock.UtcNow;
        BillingChain.Issue(invoice, project, head, ledger, terms, sequence, currentUser.UserId, now);

        audit.Record("Billing.Issued", nameof(ClientInvoice), invoice.Id, $"Issued {invoice.Number}, a {KindWords(invoice.Kind)} for {project.Name}.");
        await SaveChainChangeAsync(cancellationToken);
        return await ReadAsync(invoice.Id, await TodayAsync(cancellationToken), cancellationToken);
    }

    /// <summary>Voids an issued bill with a reason: only the project's latest bill, and only while no payment is recorded against it.</summary>
    public async Task<ClientInvoiceDto> VoidAsync(Guid id, BillingReasonRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(invoice.Version, request.Version);
        var project = await LoadProjectAsync(invoice.ProjectId, cancellationToken);
        var previous = invoice.PreviousInvoiceId is { } previousId ? await invoices.GetAsync(previousId, cancellationToken) : null;
        var latestNumber = invoice.NextInvoiceId is null ? null : (await invoices.GetChainHeadAsync(invoice.ProjectId, cancellationToken))?.Number;

        BillingChain.Void(invoice, project, previous, latestNumber, request.Reason, currentUser.UserId, clock.UtcNow);

        // The reason is free text and stays on the bill, not in the audit line.
        audit.Record("Billing.Voided", nameof(ClientInvoice), invoice.Id, $"Voided {invoice.Number} of {project.Name}.");
        await SaveChainChangeAsync(cancellationToken);
        return await ReadAsync(invoice.Id, await TodayAsync(cancellationToken), cancellationToken);
    }

    /// <summary>Discards a draft with a reason. It stays on record as void.</summary>
    public async Task<ClientInvoiceDto> DiscardAsync(Guid id, BillingReasonRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(invoice.Version, request.Version);
        var project = await LoadProjectAsync(invoice.ProjectId, cancellationToken);
        invoice.Discard(project, request.Reason, currentUser.UserId, clock.UtcNow);

        audit.Record("Billing.DraftDiscarded", nameof(ClientInvoice), invoice.Id, $"Discarded a draft {KindWords(invoice.Kind)} for {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(invoice.Id, await TodayAsync(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Records a payment the client made against an issued bill. A person says it was received; nothing is collected. A repeated request
    /// id answers with the bill the first submission paid.
    /// </summary>
    public async Task<ClientInvoiceDto> RecordPaymentAsync(Guid id, RecordClientPaymentRequest request, CancellationToken cancellationToken)
    {
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null && await invoices.FindPaymentByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await SamePaymentAsync(already.InvoiceId, id, cancellationToken);

        try
        {
            return await RecordAsync(id, request, requestId, cancellationToken);
        }
        catch (Exception ex) when (requestId is not null && ex is DomainRuleException or ConcurrencyConflictException)
        {
            // The same form sent twice at once: the first one changed the bill, so the second finds it changed or its request id taken.
            if (await invoices.FindPaymentByClientRequestAsync(requestId, cancellationToken) is { } original)
                return await SamePaymentAsync(original.InvoiceId, id, cancellationToken);
            throw;
        }
    }

    private async Task<ClientInvoiceDto> RecordAsync(Guid id, RecordClientPaymentRequest request, string? requestId, CancellationToken cancellationToken)
    {
        var invoice = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(invoice.Version, request.Version);
        var project = await LoadProjectAsync(invoice.ProjectId, cancellationToken);
        var today = await TodayAsync(cancellationToken);
        var method = Enum.TryParse<ClientPaymentMethod>(request.Method, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : (ClientPaymentMethod)(-1);

        invoice.RecordPayment(Guid.NewGuid(), project, request.AmountMinor, request.PaidOn, method, request.Reference, request.Note, currentUser.UserId, requestId,
            today, clock.UtcNow);

        audit.Record("Billing.PaymentRecorded", nameof(ClientInvoice), invoice.Id, $"Recorded a client payment against {invoice.Number} of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(invoice.Id, today, cancellationToken);
    }

    private async Task<ClientInvoiceDto> SamePaymentAsync(Guid originalInvoiceId, Guid requestedInvoiceId, CancellationToken cancellationToken) =>
        originalInvoiceId == requestedInvoiceId
            ? await ReadAsync(originalInvoiceId, await TodayAsync(cancellationToken), cancellationToken)
            : throw new DomainRuleException("That request id was already used for a payment on a different bill. Reload the page and try again.");

    /// <summary>Voids a recorded payment with a reason; the amount is owed again.</summary>
    public async Task<ClientInvoiceDto> VoidPaymentAsync(Guid id, Guid paymentId, BillingReasonRequest request, CancellationToken cancellationToken)
    {
        var invoice = await LoadAsync(id, cancellationToken);
        ConcurrencyGuard.Ensure(invoice.Version, request.Version);
        var project = await LoadProjectAsync(invoice.ProjectId, cancellationToken);
        invoice.VoidPayment(paymentId, project, request.Reason, currentUser.UserId, clock.UtcNow);

        audit.Record("Billing.PaymentVoided", nameof(ClientInvoice), invoice.Id, $"Voided a client payment against {invoice.Number} of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(invoice.Id, await TodayAsync(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Saves an issue or a void. Both change the neighbouring bill as well, so a second person doing the same at the same moment is
    /// refused by the database; say so in words that tell them what to do.
    /// </summary>
    private async Task SaveChainChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DomainRuleException or ConcurrencyConflictException)
        {
            throw new DomainRuleException(
                "Another bill for this project was issued or voided at the same moment. Reload the bill to see the latest figures, then try again.");
        }
    }

    private async Task<ClientInvoice> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await invoices.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that bill");

    private async Task<Project> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await projects.GetAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project");

    private async Task<ClientInvoiceDto> ReadAsync(Guid id, DateOnly today, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, today, cancellationToken) ?? throw new NotFoundException("that bill");

    private async Task<BillingTermsSnapshot> TermsAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await termsRepository.GetByProjectAsync(projectId, cancellationToken))?.Snapshot ?? BillingTermsSnapshot.Defaults;

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);

    private static ClientInvoiceKind ParseKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return ClientInvoiceKind.Progress;
        return Enum.TryParse<ClientInvoiceKind>(kind.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ValidationFailedException("kind", "Choose a progress bill, a down payment or a retention release.");
    }

    private static long Required(long? value, string field, string message) => value ?? throw new ValidationFailedException(field, message);

    private static string KindWords(ClientInvoiceKind kind) => kind switch
    {
        ClientInvoiceKind.Progress => "progress bill",
        ClientInvoiceKind.Advance => "down payment bill",
        _ => "retention release"
    };
}
