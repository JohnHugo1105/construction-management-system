using Cms.Application.Features.Billing;
using Cms.Domain.Features.Billing;
using Cms.Domain.Features.Organizations;
using Cms.Domain.Features.Projects;
using Cms.Infrastructure.Persistence;

namespace Cms.Infrastructure.Seeding;

/// <summary>
/// Demo client billing for Riverside Residences (fictional): saved terms (10% retention, 2% withholding, 30 days), two issued progress
/// bills (20% and 40% of the contract), the first paid in full and the second part-paid and overdue, and a draft for 50% waiting to be
/// issued. Built through the domain, so every figure follows the billing rules. Call it from the development seeder after the project
/// exists (tracked or saved); it saves its own records.
/// </summary>
public static class BillingDemo
{
    public static async Task SeedAsync(CmsDbContext db, Organization organization, Project riverside, Guid accountantUserId, Guid managerUserId,
        DateOnly today, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var tenant = organization.Id;
        var price = riverside.ContractPriceMinor ?? throw new InvalidOperationException("The billing demo needs Riverside Residences to have a contract price.");
        var terms = ProjectBillingTerms.Create(Guid.NewGuid(), tenant, riverside, 1_000, 200, 30, now);
        db.Add(terms);

        long Share(int bps) => price * bps / 10_000;

        // BILL-0001: 20% of the contract, paid in full by bank transfer.
        var first = ClientInvoice.DraftProgress(Guid.NewGuid(), tenant, riverside, today.AddDays(-75), "Progress billing no. 1: foundations and ground floor slab",
            Share(2_000), BillingLedger.Empty, terms.Snapshot, organization.VatRateBps, managerUserId, null, today, now);
        BillingChain.Issue(first, riverside, null, BillingLedger.Empty, 1, accountantUserId, now);
        first.RecordPayment(Guid.NewGuid(), riverside, first.NetReceivableMinor, today.AddDays(-48), ClientPaymentMethod.BankTransfer, "Demo transfer RLC-0001",
            null, accountantUserId, null, today, now);

        // BILL-0002: 40% to date, part-paid by cheque; the rest is past its due date.
        var afterFirst = new BillingLedger(first.GrossMinor, first.RetentionMinor, 0);
        var second = ClientInvoice.DraftProgress(Guid.NewGuid(), tenant, riverside, today.AddDays(-40), "Progress billing no. 2: tower A structure to level 5",
            Share(4_000), afterFirst, terms.Snapshot, organization.VatRateBps, managerUserId, null, today, now);
        BillingChain.Issue(second, riverside, first, afterFirst, 2, accountantUserId, now);
        second.RecordPayment(Guid.NewGuid(), riverside, 5_000_000_00, today.AddDays(-5), ClientPaymentMethod.Cheque, "Demo cheque 004512", "First instalment",
            accountantUserId, null, today, now);

        // A draft for 50% to date, waiting for an accountant to issue it.
        var afterSecond = new BillingLedger(first.GrossMinor + second.GrossMinor, first.RetentionMinor + second.RetentionMinor, 0);
        var draft = ClientInvoice.DraftProgress(Guid.NewGuid(), tenant, riverside, today.AddDays(-1), "Progress billing no. 3: tower A structure complete",
            Share(5_000), afterSecond, terms.Snapshot, organization.VatRateBps, managerUserId, null, today, now);

        db.AddRange(first, second, draft);
        // The next bill an accountant issues is BILL-0003.
        db.Add(new NumberSequenceRow { TenantId = tenant, Key = ClientInvoiceService.NumberSequenceKey, LastValue = 2 });
        await db.SaveChangesAsync(cancellationToken);
    }
}
