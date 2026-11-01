using Cms.Application.Common;
using Cms.Application.Features.Billing;
using Cms.Domain.Features.Billing;
using Cms.Domain.Features.ChangeOrders;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Organizations;
using Cms.Domain.Features.Projects;
using Cms.Infrastructure.Common;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Billing;

/// <summary>One project's billing figures, each summed in SQL from its issued bills and their payments.</summary>
internal sealed class ProjectFigures
{
    public Guid ProjectId { get; init; }
    public string Name { get; init; } = "";
    public ProjectStatus Status { get; init; }
    public long? ContractPriceMinor { get; init; }

    /// <summary>Value of work on issued progress bills (VAT included).</summary>
    public long BilledMinor { get; init; }

    public long WithheldMinor { get; init; }
    public long ReleasedMinor { get; init; }

    /// <summary>What the issued bills ask for in all, and the part of it already past its due date.</summary>
    public long ReceivableMinor { get; init; }

    public long ReceivableDueMinor { get; init; }

    /// <summary>Payments (not voided) on issued bills, and the part of them on bills past their due date.</summary>
    public long CollectedMinor { get; init; }

    public long CollectedDueMinor { get; init; }
}

/// <summary>
/// The sums every billing figure comes from, written once so the project tab, the receivables, a bill and the issue checks agree.
/// Each sum is a scalar subquery, so a read is a fixed number of statements whatever the number of bills and payments.
/// </summary>
/// <remarks>
/// A payment can't exceed what its bill still asks for, so a bill's outstanding amount is never negative. That lets "outstanding" be
/// what the issued bills ask for less what was paid on them, and "overdue" the same over the bills past their due date, without
/// working bill by bill.
/// </remarks>
internal static class BillingQueries
{
    /// <summary>
    /// What a project's issued bills add up to: work billed (progress gross), retention withheld and released, the down payment (with
    /// the rate its bill keeps; at most one is issued) and what progress bills recovered of it. One statement.
    /// </summary>
    public static async Task<BillingLedger> LedgerAsync(CmsDbContext db, Guid projectId, CancellationToken cancellationToken) =>
        await db.Set<ClientInvoice>().AsNoTracking()
            .Where(bill => bill.ProjectId == projectId && bill.Status == ClientInvoiceStatus.Issued)
            .GroupBy(bill => bill.ProjectId)
            .Select(group => new BillingLedger(
                group.Sum(bill => bill.Kind == ClientInvoiceKind.Progress ? bill.GrossMinor : 0L),
                group.Sum(bill => bill.Kind == ClientInvoiceKind.Progress ? bill.RetentionMinor : 0L),
                group.Sum(bill => bill.Kind == ClientInvoiceKind.RetentionRelease ? bill.GrossMinor : 0L),
                group.Sum(bill => bill.Kind == ClientInvoiceKind.Advance ? bill.GrossMinor : 0L),
                group.Max(bill => bill.Kind == ClientInvoiceKind.Advance ? bill.AdvanceBps : 0),
                group.Sum(bill => bill.Kind == ClientInvoiceKind.Progress ? bill.AdvanceRecoveryMinor : 0L)))
            .SingleOrDefaultAsync(cancellationToken) ?? BillingLedger.Empty;

    /// <summary>Each project's figures. Filter the projects first; every figure is a subquery on that project's bills.</summary>
    public static IQueryable<ProjectFigures> Figures(CmsDbContext db, IQueryable<Project> projects, DateOnly today)
    {
        var issued = db.Set<ClientInvoice>().Where(bill => bill.Status == ClientInvoiceStatus.Issued);
        var payments = from payment in db.Set<ClientPayment>()
                       where !payment.IsVoided
                       join bill in issued on payment.ClientInvoiceId equals bill.Id
                       select new { bill.ProjectId, bill.DueDate, payment.AmountMinor };

        return projects.Select(project => new ProjectFigures
        {
            ProjectId = project.Id,
            Name = project.Name,
            Status = project.Status,
            ContractPriceMinor = project.ContractPriceMinor,
            BilledMinor = issued.Where(bill => bill.ProjectId == project.Id && bill.Kind == ClientInvoiceKind.Progress).Sum(bill => bill.GrossMinor),
            WithheldMinor = issued.Where(bill => bill.ProjectId == project.Id && bill.Kind == ClientInvoiceKind.Progress).Sum(bill => bill.RetentionMinor),
            ReleasedMinor = issued.Where(bill => bill.ProjectId == project.Id && bill.Kind == ClientInvoiceKind.RetentionRelease).Sum(bill => bill.GrossMinor),
            ReceivableMinor = issued.Where(bill => bill.ProjectId == project.Id).Sum(bill => bill.NetReceivableMinor),
            ReceivableDueMinor = issued.Where(bill => bill.ProjectId == project.Id && bill.DueDate < today).Sum(bill => bill.NetReceivableMinor),
            CollectedMinor = payments.Where(row => row.ProjectId == project.Id).Sum(row => row.AmountMinor),
            CollectedDueMinor = payments.Where(row => row.ProjectId == project.Id && row.DueDate < today).Sum(row => row.AmountMinor)
        });
    }
}

internal sealed class ProjectBillingTermsRepository(CmsDbContext db) : IProjectBillingTermsRepository
{
    public Task<ProjectBillingTerms?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        db.Set<ProjectBillingTerms>().SingleOrDefaultAsync(terms => terms.ProjectId == projectId, cancellationToken);

    public void Add(ProjectBillingTerms terms) => db.Set<ProjectBillingTerms>().Add(terms);
}

internal sealed class ClientInvoiceRepository(CmsDbContext db) : IClientInvoiceRepository
{
    public Task<ClientInvoice?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<ClientInvoice>().Include(bill => bill.Payments).SingleOrDefaultAsync(bill => bill.Id == id, cancellationToken);

    public Task<ClientInvoice?> GetChainHeadAsync(Guid projectId, CancellationToken cancellationToken) =>
        db.Set<ClientInvoice>().Include(bill => bill.Payments)
            .SingleOrDefaultAsync(bill => bill.ProjectId == projectId && bill.Status == ClientInvoiceStatus.Issued && bill.NextInvoiceId == null,
                cancellationToken);

    public Task<BillingLedger> GetLedgerAsync(Guid projectId, CancellationToken cancellationToken) => BillingQueries.LedgerAsync(db, projectId, cancellationToken);

    public Task<bool> AnyLiveAdvanceAsync(Guid projectId, CancellationToken cancellationToken) =>
        db.Set<ClientInvoice>().AnyAsync(bill => bill.ProjectId == projectId && bill.Kind == ClientInvoiceKind.Advance &&
            bill.Status != ClientInvoiceStatus.Void, cancellationToken);

    public Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<ClientInvoice>().Where(bill => bill.ClientRequestId == clientRequestId).Select(bill => (Guid?)bill.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<(Guid InvoiceId, Guid PaymentId)?> FindPaymentByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken)
    {
        var found = await db.Set<ClientPayment>().Where(payment => payment.ClientRequestId == clientRequestId)
            .Select(payment => new { payment.ClientInvoiceId, payment.Id }).SingleOrDefaultAsync(cancellationToken);
        return found is null ? null : (found.ClientInvoiceId, found.Id);
    }

    public void Add(ClientInvoice invoice) => db.Set<ClientInvoice>().Add(invoice);
}

internal sealed class BillingReader(CmsDbContext db) : IBillingReader
{
    private const int MaximumPaymentsShown = ClientInvoice.MaximumPayments;

    public async Task<ProjectBillingDto?> GetProjectBillingAsync(Guid projectId, DateOnly today, PageRequest page, CancellationToken cancellationToken)
    {
        var project = await BillingQueries.Figures(db, db.Set<Project>().AsNoTracking().Where(item => item.Id == projectId), today)
            .Select(figures => new
            {
                Figures = figures,
                Terms = db.Set<ProjectBillingTerms>().Where(terms => terms.ProjectId == figures.ProjectId)
                    .Select(terms => new
                    {
                        terms.RetentionBps, terms.CreditableWithholdingBps, terms.PaymentTermsDays, terms.AdvanceBps, terms.VatWithholdingBps,
                        terms.IsGovernmentClient, terms.Version
                    })
                    .FirstOrDefault(),
                VatRateBps = db.Set<Organization>().Select(organization => organization.VatRateBps).FirstOrDefault(),
                OverdueCount = db.Set<ClientInvoice>().Count(bill => bill.ProjectId == figures.ProjectId && bill.Status == ClientInvoiceStatus.Issued &&
                    bill.DueDate < today &&
                    bill.NetReceivableMinor > db.Set<ClientPayment>().Where(payment => payment.ClientInvoiceId == bill.Id && !payment.IsVoided)
                        .Sum(payment => payment.AmountMinor)),
                // The down payment: the live (draft or issued) bill, what was issued and what issued progress bills recovered of it.
                AdvanceBillId = db.Set<ClientInvoice>()
                    .Where(bill => bill.ProjectId == figures.ProjectId && bill.Kind == ClientInvoiceKind.Advance && bill.Status != ClientInvoiceStatus.Void)
                    .Select(bill => (Guid?)bill.Id).FirstOrDefault(),
                AdvanceBilled = db.Set<ClientInvoice>()
                    .Where(bill => bill.ProjectId == figures.ProjectId && bill.Kind == ClientInvoiceKind.Advance && bill.Status == ClientInvoiceStatus.Issued)
                    .Sum(bill => bill.GrossMinor),
                AdvanceRecovered = db.Set<ClientInvoice>()
                    .Where(bill => bill.ProjectId == figures.ProjectId && bill.Kind == ClientInvoiceKind.Progress && bill.Status == ClientInvoiceStatus.Issued)
                    .Sum(bill => bill.AdvanceRecoveryMinor)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;

        var figures = project.Figures;
        var held = figures.WithheldMinor - figures.ReleasedMinor;
        var bills = await ListAsync(new ClientInvoiceListQuery(projectId, null, false, null), today, page, cancellationToken);
        var defaults = BillingTermsSnapshot.Defaults;
        var terms = project.Terms is { } saved
            ? new BillingTermsDto(saved.RetentionBps, saved.CreditableWithholdingBps, saved.PaymentTermsDays, saved.AdvanceBps, saved.VatWithholdingBps,
                saved.IsGovernmentClient, false, saved.Version)
            : new BillingTermsDto(defaults.RetentionBps, defaults.WithholdingBps, defaults.PaymentTermsDays, defaults.AdvanceBps, defaults.VatWithholdingBps,
                false, true, 0);

        // A down payment bill can be prepared while the terms take one, none is live, nothing is billed for progress and there is a price.
        long? advanceToBill = terms.AdvanceBps > 0 && project.AdvanceBillId is null && figures.BilledMinor == 0 && figures.ContractPriceMinor is > 0
            ? BillingCalculator.Advance(figures.ContractPriceMinor.Value, terms.AdvanceBps).GrossMinor
            : null;

        return new ProjectBillingDto(projectId, figures.Name, figures.Status.ToString(), figures.ContractPriceMinor, figures.BilledMinor,
            figures.ContractPriceMinor is { } price ? BillingCalculator.ShareBps(figures.BilledMinor, price) : null,
            figures.ContractPriceMinor is { } contract ? Math.Max(0, contract - figures.BilledMinor) : null,
            figures.WithheldMinor, figures.ReleasedMinor, held, figures.CollectedMinor, figures.ReceivableMinor - figures.CollectedMinor,
            figures.ReceivableDueMinor - figures.CollectedDueMinor, project.OverdueCount, figures.Status == ProjectStatus.Completed && held > 0,
            project.AdvanceBilled, project.AdvanceRecovered, project.AdvanceBilled - project.AdvanceRecovered, project.AdvanceBillId,
            advanceToBill is > 0 ? advanceToBill : null, terms, project.VatRateBps, bills);
    }

    public async Task<PagedResult<ClientInvoiceListItemDto>> ListAsync(ClientInvoiceListQuery query, DateOnly today, PageRequest page,
        CancellationToken cancellationToken)
    {
        var bills = db.Set<ClientInvoice>().AsNoTracking();
        if (query.ProjectId is { } projectId) bills = bills.Where(bill => bill.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ClientInvoiceStatus>(query.Status, true, out var status) && Enum.IsDefined(status))
            bills = bills.Where(bill => bill.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = Sql.Contains(query.Search);
            bills = bills.Where(bill => (bill.Number != null && EF.Functions.Like(bill.Number, pattern, Sql.LikeEscape)) ||
                EF.Functions.Like(bill.Description, pattern, Sql.LikeEscape));
        }
        if (query.OverdueOnly)
            bills = bills.Where(bill => bill.Status == ClientInvoiceStatus.Issued && bill.DueDate < today &&
                bill.NetReceivableMinor > db.Set<ClientPayment>().Where(payment => payment.ClientInvoiceId == bill.Id && !payment.IsVoided)
                    .Sum(payment => payment.AmountMinor));

        var total = await bills.CountAsync(cancellationToken);
        // Drafts first (they need someone), then the newest bills.
        var rows = await (
            from bill in bills
            join project in db.Set<Project>() on bill.ProjectId equals project.Id
            orderby bill.Status == ClientInvoiceStatus.Draft ? 0 : 1, bill.BillingDate descending, bill.CreatedAtUtc descending, bill.Id
            select new
            {
                bill.Id,
                bill.Number,
                bill.ProjectId,
                ProjectName = project.Name,
                bill.Kind,
                bill.Status,
                bill.BillingDate,
                bill.DueDate,
                bill.Description,
                bill.GrossMinor,
                bill.NetReceivableMinor,
                bill.Version,
                Paid = db.Set<ClientPayment>().Where(payment => payment.ClientInvoiceId == bill.Id && !payment.IsVoided).Sum(payment => payment.AmountMinor)
            })
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<ClientInvoiceListItemDto>.Of(rows.Select(row =>
        {
            var issued = row.Status == ClientInvoiceStatus.Issued;
            var outstanding = issued ? row.NetReceivableMinor - row.Paid : 0;
            return new ClientInvoiceListItemDto(row.Id, row.Number, row.ProjectId, row.ProjectName, row.Kind.ToString(), row.Status.ToString(), row.BillingDate,
                row.DueDate, row.Description, row.GrossMinor, row.NetReceivableMinor, row.Paid, outstanding, issued && outstanding > 0 && today > row.DueDate,
                row.Version);
        }).ToArray(), total, page);
    }

    public async Task<ClientInvoiceDto?> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken)
    {
        var bill = await (
            from item in db.Set<ClientInvoice>().AsNoTracking()
            where item.Id == id
            join project in db.Set<Project>() on item.ProjectId equals project.Id
            select new
            {
                Bill = item,
                ProjectName = project.Name,
                ProjectStatus = project.Status,
                project.ContractPriceMinor,
                CreatedBy = db.Set<User>().Where(user => user.Id == item.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                IssuedBy = db.Set<User>().Where(user => user.Id == item.IssuedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                VoidedBy = db.Set<User>().Where(user => user.Id == item.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).SingleOrDefaultAsync(cancellationToken);
        if (bill is null) return null;

        var payments = await (
            from payment in db.Set<ClientPayment>().AsNoTracking()
            where payment.ClientInvoiceId == id
            orderby payment.PaidOn descending, payment.RecordedAtUtc descending
            select new
            {
                payment.Id,
                payment.AmountMinor,
                payment.PaidOn,
                payment.Method,
                payment.Reference,
                payment.Note,
                payment.RecordedAtUtc,
                payment.IsVoided,
                payment.VoidedAtUtc,
                payment.VoidReason,
                RecordedBy = db.Set<User>().Where(user => user.Id == payment.RecordedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                VoidedBy = db.Set<User>().Where(user => user.Id == payment.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).Take(MaximumPaymentsShown).ToListAsync(cancellationToken);

        var invoice = bill.Bill;
        // A draft's figures were worked out from what was billed when it was saved; say when that is no longer true.
        var stale = invoice.Status == ClientInvoiceStatus.Draft &&
            invoice.IsStale(await BillingQueries.LedgerAsync(db, invoice.ProjectId, cancellationToken), bill.ContractPriceMinor);
        var paid = payments.Where(payment => !payment.IsVoided).Sum(payment => payment.AmountMinor);
        var issued = invoice.Status == ClientInvoiceStatus.Issued;
        var outstanding = issued ? invoice.NetReceivableMinor - paid : 0;

        return new ClientInvoiceDto(invoice.Id, invoice.Number, invoice.ProjectId, bill.ProjectName, bill.ProjectStatus.ToString(), bill.ContractPriceMinor,
            invoice.Kind.ToString(), invoice.Status.ToString(), invoice.BillingDate, invoice.DueDate, invoice.Description, invoice.CumulativeWorkMinor,
            invoice.PreviousCumulativeWorkMinor, invoice.GrossMinor, invoice.NetMinor, invoice.VatMinor, invoice.VatRateBps, invoice.RetentionBps,
            invoice.RetentionMinor, invoice.WithholdingBps, invoice.WithholdingMinor, invoice.VatWithholdingBps, invoice.VatWithholdingMinor,
            invoice.AdvanceBps, invoice.AdvanceRecoveryMinor, invoice.NetReceivableMinor, invoice.PaymentTermsDays, paid, outstanding,
            issued && outstanding > 0 && today > invoice.DueDate, issued && invoice.NextInvoiceId is null, stale, bill.CreatedBy, invoice.CreatedAtUtc,
            bill.IssuedBy, invoice.IssuedAtUtc, bill.VoidedBy, invoice.VoidedAtUtc, invoice.VoidReason,
            payments.Select(payment => new ClientPaymentDto(payment.Id, payment.AmountMinor, payment.PaidOn, payment.Method.ToString(), payment.Reference,
                payment.Note, payment.RecordedBy, payment.RecordedAtUtc, payment.IsVoided, payment.VoidedBy, payment.VoidedAtUtc, payment.VoidReason)).ToArray(),
            invoice.Version);
    }

    public async Task<ReceivablesDto> GetReceivablesAsync(DateOnly today, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = BillingQueries.Figures(db, db.Set<Project>().AsNoTracking(), today)
            .Where(row => row.ReceivableMinor > row.CollectedMinor || row.WithheldMinor > row.ReleasedMinor);

        var total = await rows.CountAsync(cancellationToken);
        // Overdue money first, then the most still to collect.
        var pageRows = await rows
            .OrderByDescending(row => row.ReceivableDueMinor - row.CollectedDueMinor)
            .ThenByDescending(row => row.ReceivableMinor - row.CollectedMinor)
            .ThenBy(row => row.Name).ThenBy(row => row.ProjectId)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);

        // The company's totals over every issued bill. A project left out of the rows owes nothing and holds no retention, so these are
        // also the totals of the rows. One statement: each total is a subquery on the company's single organization row.
        var issued = db.Set<ClientInvoice>().Where(bill => bill.Status == ClientInvoiceStatus.Issued);
        var paid = from payment in db.Set<ClientPayment>()
                   where !payment.IsVoided
                   join bill in issued on payment.ClientInvoiceId equals bill.Id
                   select new { bill.DueDate, payment.AmountMinor };
        var totals = await db.Set<Organization>().AsNoTracking().Select(organization => new
        {
            Receivable = issued.Sum(bill => bill.NetReceivableMinor),
            ReceivableDue = issued.Where(bill => bill.DueDate < today).Sum(bill => bill.NetReceivableMinor),
            Collected = paid.Sum(row => row.AmountMinor),
            CollectedDue = paid.Where(row => row.DueDate < today).Sum(row => row.AmountMinor),
            Withheld = issued.Where(bill => bill.Kind == ClientInvoiceKind.Progress).Sum(bill => bill.RetentionMinor),
            Released = issued.Where(bill => bill.Kind == ClientInvoiceKind.RetentionRelease).Sum(bill => bill.GrossMinor)
        }).FirstOrDefaultAsync(cancellationToken);

        var items = pageRows.Select(row => new ReceivableRowDto(row.ProjectId, row.Name, row.Status.ToString(), row.BilledMinor, row.CollectedMinor,
            row.ReceivableMinor - row.CollectedMinor, row.ReceivableDueMinor - row.CollectedDueMinor, row.WithheldMinor - row.ReleasedMinor)).ToArray();
        return new ReceivablesDto(totals is null ? 0 : totals.Receivable - totals.Collected, totals is null ? 0 : totals.ReceivableDue - totals.CollectedDue,
            totals is null ? 0 : totals.Withheld - totals.Released, PagedResult<ReceivableRowDto>.Of(items, total, page));
    }
}

/// <summary>A government client's limit on added variation orders: the terms' flag, the baseline price and the approved increases, in one statement.</summary>
internal sealed class GovernmentLimitReader(CmsDbContext db) : IGovernmentLimitReader
{
    public Task<GovernmentLimitFigures?> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        db.Set<Project>().AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new GovernmentLimitFigures(
                db.Set<ProjectBillingTerms>().Where(terms => terms.ProjectId == project.Id).Select(terms => terms.IsGovernmentClient).FirstOrDefault(),
                project.BaselineContractPriceMinor,
                db.Set<ChangeOrder>()
                    .Where(order => order.ProjectId == project.Id && order.Status == ChangeOrderStatus.Approved && order.ContractPriceDeltaMinor > 0)
                    .Sum(order => order.ContractPriceDeltaMinor)))
            .SingleOrDefaultAsync(cancellationToken);
}
