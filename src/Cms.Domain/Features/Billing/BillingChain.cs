using Cms.Domain.Features.Projects;

namespace Cms.Domain.Features.Billing;

/// <summary>
/// Issues and voids bills while keeping a project's issued bills one unbroken chain. Each progress bill bills the difference between
/// its work to date and the previous issued bill's, so the chain is what makes "billed to date" true: an issued bill must follow the
/// latest one, and only the latest one can be voided. Both steps also change the neighbouring bill, which is what stops two people
/// doing them at the same moment (see <see cref="ClientInvoice"/>).
/// </summary>
public static class BillingChain
{
    /// <param name="head">The project's latest issued bill (it has no successor), or null when none is issued.</param>
    /// <param name="ledger">What the project's issued bills add up to now; the draft's figures must still follow from it.</param>
    /// <param name="terms">The project's billing terms now; a down payment draft must still follow from them.</param>
    public static void Issue(ClientInvoice invoice, Project project, ClientInvoice? head, BillingLedger ledger, BillingTermsSnapshot terms, long sequenceNumber,
        Guid issuedByUserId, DateTimeOffset now)
    {
        if (head is not null &&
            (head.ProjectId != invoice.ProjectId || head.Status != ClientInvoiceStatus.Issued || head.NextInvoiceId is not null || head.ChainIndex is null))
            throw new InvalidOperationException("The bill to follow must be the project's latest issued bill.");

        invoice.MarkIssued(project, ledger, terms, sequenceNumber, (head?.ChainIndex ?? 0) + 1, head?.Id, issuedByUserId, now);
        head?.LinkSuccessor(invoice.Id, now);
    }

    /// <param name="previous">The issued bill this one follows, or null when it is the project's first.</param>
    /// <param name="latestNumber">The number of the project's latest issued bill, to name it when this one isn't the latest.</param>
    public static void Void(ClientInvoice invoice, Project project, ClientInvoice? previous, string? latestNumber, string? reason, Guid voidedByUserId,
        DateTimeOffset now)
    {
        if (previous?.Id != invoice.PreviousInvoiceId) throw new InvalidOperationException("That is not the bill this one follows.");

        invoice.MarkVoided(project, latestNumber, reason, voidedByUserId, now);
        previous?.UnlinkSuccessor(invoice.Id, now);
    }
}
