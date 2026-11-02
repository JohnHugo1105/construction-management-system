using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Domain.Features.Billing;
using ConstructOPX.Domain.Features.Bonds;
using ConstructOPX.Domain.Features.ChangeOrders;
using ConstructOPX.Domain.Features.Payroll;
using ConstructOPX.Domain.Features.Progress;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Purchasing;
using ConstructOPX.Domain.Features.Subcontracts;
using ConstructOPX.Domain.Features.Suppliers;
using ConstructOPX.Domain.Features.Workforce;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.NeedsAction;

/// <summary>
/// Reads the waiting work of a group straight from the records' statuses. A group's count and its rows share one filter (the private
/// query per group), so they never disagree; each is one SQL statement, whatever the number of records. Every set here carries the
/// company filter, and every project-bound group leaves out archived projects. Rows select only the fields the group shows.
/// </summary>
internal sealed class NeedsActionReader(ConstructOpxDbContext db) : INeedsActionReader
{
    public Task<int> CountAsync(NeedsActionGroup group, NeedsActionDates dates, CancellationToken cancellationToken) => group switch
    {
        NeedsActionGroup.PurchaseOrdersToApprove => DraftOrders().CountAsync(cancellationToken),
        NeedsActionGroup.ChangeOrdersToApprove => SubmittedChangeOrders().CountAsync(cancellationToken),
        NeedsActionGroup.BillsToIssue => DraftBills().CountAsync(cancellationToken),
        NeedsActionGroup.ClaimsToCertify => DraftClaims().CountAsync(cancellationToken),
        NeedsActionGroup.PayrollToApprove => DraftPayrolls().CountAsync(cancellationToken),
        NeedsActionGroup.BillsToChase => OverdueBills(dates).CountAsync(cancellationToken),
        NeedsActionGroup.BondsToAttend => BondsNeedingAttention(dates).CountAsync(cancellationToken),
        NeedsActionGroup.ProgressNotUpdated => StaleProgress(dates).CountAsync(cancellationToken),
        NeedsActionGroup.AttendanceNotRecorded => MissingAttendance(dates).CountAsync(cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "This group is not read here.")
    };

    public async Task<IReadOnlyList<NeedsActionRecord>> TopAsync(NeedsActionGroup group, NeedsActionDates dates, int take,
        CancellationToken cancellationToken) => group switch
    {
        // The longest waiting first.
        NeedsActionGroup.PurchaseOrdersToApprove => await DraftOrders()
            .OrderBy(row => row.Order.CreatedAtUtc).ThenBy(row => row.Order.Id).Take(take)
            .Select(row => new NeedsActionRecord(row.Order.Id, null, row.Order.Number,
                db.Set<Supplier>().Where(supplier => supplier.Id == row.Order.SupplierId).Select(supplier => supplier.Name).FirstOrDefault(),
                null, row.Project.Id, row.Project.Name, row.Order.CreatedAtUtc, null, null, row.Order.GrossMinor, null))
            .ToListAsync(cancellationToken),
        NeedsActionGroup.ChangeOrdersToApprove => await SubmittedChangeOrders()
            .OrderBy(row => row.Order.SubmittedAtUtc ?? row.Order.CreatedAtUtc).ThenBy(row => row.Order.Id).Take(take)
            .Select(row => new NeedsActionRecord(row.Order.Id, null, row.Order.Number, row.Order.Title, null, row.Project.Id, row.Project.Name,
                row.Order.SubmittedAtUtc ?? row.Order.CreatedAtUtc, null, null, row.Order.BudgetEffectGrossMinor, null))
            .ToListAsync(cancellationToken),
        NeedsActionGroup.BillsToIssue => await DraftBills()
            .OrderBy(row => row.Bill.CreatedAtUtc).ThenBy(row => row.Bill.Id).Take(take)
            .Select(row => new NeedsActionRecord(row.Bill.Id, null, null, row.Project.Client, row.Bill.Kind.ToString(), row.Project.Id, row.Project.Name,
                row.Bill.CreatedAtUtc, row.Bill.BillingDate, null, row.Bill.NetReceivableMinor, null))
            .ToListAsync(cancellationToken),
        NeedsActionGroup.ClaimsToCertify => await DraftClaims()
            .OrderBy(row => row.Claim.CreatedAtUtc).ThenBy(row => row.Claim.Id).Take(take)
            .Select(row => new NeedsActionRecord(row.Claim.Id, row.Subcontract.Id, row.Subcontract.Number,
                db.Set<Supplier>().Where(supplier => supplier.Id == row.Subcontract.SupplierId).Select(supplier => supplier.Name).FirstOrDefault(),
                null, row.Project.Id, row.Project.Name, row.Claim.CreatedAtUtc, row.Claim.PeriodEnd, null, row.Claim.GrossMinor, null))
            .ToListAsync(cancellationToken),
        // No pay figure is selected: the period, the kind and how many workers it pays.
        NeedsActionGroup.PayrollToApprove => await DraftPayrolls()
            .OrderBy(run => run.CreatedAtUtc).ThenBy(run => run.Id).Take(take)
            .Select(run => new NeedsActionRecord(run.Id, null, null, null, run.Kind.ToString(), null, null, run.CreatedAtUtc, run.PeriodStart, run.PeriodEnd,
                null, db.Set<PayrollLine>().Count(line => line.PayrollRunId == run.Id)))
            .ToListAsync(cancellationToken),
        // The most overdue first.
        NeedsActionGroup.BillsToChase => await OverdueBills(dates)
            .OrderBy(row => row.Bill.DueDate).ThenBy(row => row.Bill.Number).ThenBy(row => row.Bill.Id).Take(take)
            .Select(row => new NeedsActionRecord(row.Bill.Id, null, row.Bill.Number, row.Project.Client, row.Bill.Kind.ToString(), row.Project.Id,
                row.Project.Name, row.Bill.IssuedAtUtc, row.Bill.DueDate, null, row.Bill.NetReceivableMinor - row.Paid, null))
            .ToListAsync(cancellationToken),
        // Expired first (the earliest valid-until date), then the soonest to lapse.
        NeedsActionGroup.BondsToAttend => await BondsNeedingAttention(dates)
            .OrderBy(row => row.Bond.ValidUntil).ThenBy(row => row.Bond.BondNumber).ThenBy(row => row.Bond.Id).Take(take)
            .Select(row => new NeedsActionRecord(row.Bond.Id, null, row.Bond.BondNumber, row.Bond.IssuerName, row.Bond.Kind.ToString(), row.Project.Id,
                row.Project.Name, null, row.Bond.ValidUntil, null, row.Bond.AmountMinor, null))
            .ToListAsync(cancellationToken),
        // The longest without progress first.
        NeedsActionGroup.ProgressNotUpdated => await StaleProgress(dates)
            .OrderBy(row => row.Latest ?? row.Project.StartDate).ThenBy(row => row.Project.Name).ThenBy(row => row.Project.Id).Take(take)
            .Select(row => new NeedsActionRecord(row.Project.Id, null, null, null, null, row.Project.Id, row.Project.Name, null, row.Latest,
                row.Project.StartDate, null, null))
            .ToListAsync(cancellationToken),
        // The most workers without an entry first.
        NeedsActionGroup.AttendanceNotRecorded => await MissingAttendance(dates)
            .OrderByDescending(row => row.Missing).ThenBy(row => row.Project.Name).ThenBy(row => row.Project.Id).Take(take)
            .Select(row => new NeedsActionRecord(row.Project.Id, null, null, null, null, row.Project.Id, row.Project.Name, null, dates.AttendanceDay, null,
                null, row.Missing))
            .ToListAsync(cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "This group is not read here.")
    };

    // ---- One filter per group ---------------------------------------------------------------------------------------------

    private IQueryable<Project> LiveProjects() => db.Set<Project>().AsNoTracking().Where(project => project.Status != ProjectStatus.Archived);

    private IQueryable<OrderRow> DraftOrders() =>
        from order in db.Set<PurchaseOrder>().AsNoTracking()
        where order.Status == PurchaseOrderStatus.Draft
        join project in LiveProjects() on order.ProjectId equals project.Id
        select new OrderRow { Order = order, Project = project };

    private IQueryable<ChangeOrderRow> SubmittedChangeOrders() =>
        from order in db.Set<ChangeOrder>().AsNoTracking()
        where order.Status == ChangeOrderStatus.Submitted
        join project in LiveProjects() on order.ProjectId equals project.Id
        select new ChangeOrderRow { Order = order, Project = project };

    /// <summary>Draft bills; a discarded draft is Void and is not one.</summary>
    private IQueryable<BillRow> DraftBills() =>
        from bill in db.Set<ClientInvoice>().AsNoTracking()
        where bill.Status == ClientInvoiceStatus.Draft
        join project in LiveProjects() on bill.ProjectId equals project.Id
        select new BillRow { Bill = bill, Project = project };

    private IQueryable<ClaimRow> DraftClaims() =>
        from claim in db.Set<SubcontractClaim>().AsNoTracking()
        where claim.Status == SubcontractClaimStatus.Draft
        join subcontract in db.Set<Subcontract>() on claim.SubcontractId equals subcontract.Id
        join project in LiveProjects() on subcontract.ProjectId equals project.Id
        select new ClaimRow { Claim = claim, Subcontract = subcontract, Project = project };

    /// <summary>Draft payrolls, 13th-month ones included. A payroll is the company's, not a project's.</summary>
    private IQueryable<PayrollRun> DraftPayrolls() => db.Set<PayrollRun>().AsNoTracking().Where(run => run.Status == PayrollRunStatus.Draft);

    /// <summary>Issued bills past their due date with something still outstanding (payments that stand count).</summary>
    private IQueryable<BillRow> OverdueBills(NeedsActionDates dates) =>
        from bill in db.Set<ClientInvoice>().AsNoTracking()
        where bill.Status == ClientInvoiceStatus.Issued && bill.DueDate < dates.Today
        join project in LiveProjects() on bill.ProjectId equals project.Id
        let paid = db.Set<ClientPayment>().Where(payment => payment.ClientInvoiceId == bill.Id && !payment.IsVoided).Sum(payment => payment.AmountMinor)
        where bill.NetReceivableMinor > paid
        select new BillRow { Bill = bill, Project = project, Paid = paid };

    /// <summary>Active bonds that have expired or expire within the bond rule's 30 days.</summary>
    private IQueryable<BondRow> BondsNeedingAttention(NeedsActionDates dates) =>
        from bond in db.Set<Bond>().AsNoTracking()
        where bond.Status == BondStatus.Active && bond.ValidUntil <= dates.BondsNeedAttentionUntil
        join project in LiveProjects() on bond.ProjectId equals project.Id
        select new BondRow { Bond = bond, Project = project };

    /// <summary>Active projects whose latest progress (or, with none, start) is more than 14 days old.</summary>
    private IQueryable<ProgressRow> StaleProgress(NeedsActionDates dates) =>
        from project in db.Set<Project>().AsNoTracking()
        where project.Status == ProjectStatus.Active
        let latest = db.Set<ProgressEntry>().Where(entry => entry.ProjectId == project.Id).Max(entry => (DateOnly?)entry.AsOfDate)
        where (latest ?? project.StartDate) < dates.ProgressStaleBefore
        select new ProgressRow { Project = project, Latest = latest };

    /// <summary>
    /// Active projects that recorded attendance on one of the six days before yesterday, when yesterday is not a company holiday, with the
    /// number of workers posted to them yesterday (active, and not on their rest day) who have no entry for yesterday.
    /// </summary>
    private IQueryable<AttendanceRow> MissingAttendance(NeedsActionDates dates)
    {
        var day = dates.AttendanceDay;
        DayOfWeek? weekday = day.DayOfWeek;
        return from project in db.Set<Project>().AsNoTracking()
               where project.Status == ProjectStatus.Active &&
                   !db.Set<CompanyHoliday>().Any(holiday => holiday.Date == day) &&
                   db.Set<AttendanceEntry>().Any(entry => entry.ProjectId == project.Id && entry.Status == AttendanceStatus.Recorded &&
                       entry.WorkDate >= dates.AttendanceLookbackFrom && entry.WorkDate <= dates.AttendanceLookbackTo)
               let missing = db.Set<Worker>().Count(worker => worker.Status == WorkerStatus.Active &&
                   (worker.RestDay == null || worker.RestDay != weekday) &&
                   db.Set<WorkerAssignment>().Any(assignment => assignment.WorkerId == worker.Id && assignment.ProjectId == project.Id &&
                       assignment.StartDate <= day && (assignment.EndDate == null || day < assignment.EndDate)) &&
                   !db.Set<AttendanceEntry>().Any(entry => entry.WorkerId == worker.Id && entry.ProjectId == project.Id && entry.WorkDate == day &&
                       entry.Status == AttendanceStatus.Recorded))
               where missing > 0
               select new AttendanceRow { Project = project, Missing = missing };
    }

    // Rows are built with object initializers (not constructors) so EF can keep composing the query after them.
    private sealed class OrderRow { public required PurchaseOrder Order { get; init; } public required Project Project { get; init; } }

    private sealed class ChangeOrderRow { public required ChangeOrder Order { get; init; } public required Project Project { get; init; } }

    private sealed class BillRow
    {
        public required ClientInvoice Bill { get; init; }
        public required Project Project { get; init; }
        public long Paid { get; init; }
    }

    private sealed class ClaimRow
    {
        public required SubcontractClaim Claim { get; init; }
        public required Subcontract Subcontract { get; init; }
        public required Project Project { get; init; }
    }

    private sealed class BondRow { public required Bond Bond { get; init; } public required Project Project { get; init; } }

    private sealed class ProgressRow { public required Project Project { get; init; } public DateOnly? Latest { get; init; } }

    private sealed class AttendanceRow { public required Project Project { get; init; } public int Missing { get; init; } }
}
