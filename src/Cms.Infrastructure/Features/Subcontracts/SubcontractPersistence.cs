using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Subcontracts;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Subcontracts;
using Cms.Domain.Features.Suppliers;
using Cms.Infrastructure.Common;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Features.Subcontracts;

internal sealed class SubcontractRepository(CmsDbContext db) : ISubcontractRepository
{
    public Task<Subcontract?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Subcontract>()
            .Include(item => item.Claims)
            .Include(item => item.Payments)
            .Include(item => item.Variations)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<RequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<Subcontract>().AsNoTracking().Where(item => item.ClientRequestId == clientRequestId)
            .Select(item => new RequestOwner(item.Id, item.ProjectId, null, null)).SingleOrDefaultAsync(cancellationToken);

    public Task<RequestOwner?> FindClaimByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        (from claim in db.Set<SubcontractClaim>().AsNoTracking()
         where claim.ClientRequestId == clientRequestId
         join subcontract in db.Set<Subcontract>() on claim.SubcontractId equals subcontract.Id
         select new RequestOwner(subcontract.Id, subcontract.ProjectId, claim.Id, claim.Id)).SingleOrDefaultAsync(cancellationToken);

    public Task<RequestOwner?> FindPaymentByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        (from payment in db.Set<SubcontractPayment>().AsNoTracking()
         where payment.ClientRequestId == clientRequestId
         join subcontract in db.Set<Subcontract>() on payment.SubcontractId equals subcontract.Id
         select new RequestOwner(subcontract.Id, subcontract.ProjectId, payment.Id, payment.ClaimId)).SingleOrDefaultAsync(cancellationToken);

    public void Add(Subcontract subcontract) => db.Set<Subcontract>().Add(subcontract);
}

/// <summary>
/// Reads subcontracts straight into DTOs. The list is two statements (count and page) and a subcontract is four (the subcontract, its claims,
/// payments and variations), however many rows exist: the totals are columns the domain keeps, never sums over children.
/// </summary>
internal sealed class SubcontractReader(CmsDbContext db) : ISubcontractReader
{
    public async Task<PagedResult<SubcontractListItemDto>> ListAsync(SubcontractListFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var rows =
            from subcontract in db.Set<Subcontract>().AsNoTracking()
            join project in db.Set<Project>() on subcontract.ProjectId equals project.Id
            join firm in db.Set<Supplier>() on subcontract.SupplierId equals firm.Id
            select new { Subcontract = subcontract, ProjectName = project.Name, FirmName = firm.Name };

        if (filter.ProjectId is { } projectId) rows = rows.Where(row => row.Subcontract.ProjectId == projectId);
        if (filter.SubcontractorId is { } firmId) rows = rows.Where(row => row.Subcontract.SupplierId == firmId);
        if (filter.Status is { } status) rows = rows.Where(row => row.Subcontract.Status == status);
        if (filter.Search is { } search)
        {
            var pattern = Sql.Contains(search);
            rows = rows.Where(row => EF.Functions.Like(row.Subcontract.Number, pattern, Sql.LikeEscape) ||
                EF.Functions.Like(row.Subcontract.Title, pattern, Sql.LikeEscape) || EF.Functions.Like(row.FirmName, pattern, Sql.LikeEscape));
        }

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(row => row.Subcontract.CreatedAtUtc).ThenByDescending(row => row.Subcontract.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(row => new
            {
                row.Subcontract.Id,
                row.Subcontract.Number,
                row.Subcontract.Title,
                row.Subcontract.SupplierId,
                row.FirmName,
                row.Subcontract.ProjectId,
                row.ProjectName,
                SubProjectName = db.Set<SubProject>().Where(sub => sub.Id == row.Subcontract.SubProjectId).Select(sub => sub.Name).FirstOrDefault(),
                row.Subcontract.Status,
                row.Subcontract.VatTreatment,
                row.Subcontract.RevisedSumMinor,
                row.Subcontract.CertifiedWorkMinor,
                row.Subcontract.RetentionHeldMinor,
                row.Subcontract.RetentionReleasedMinor,
                row.Subcontract.NetPayableMinor,
                row.Subcontract.PaidOnClaimsMinor,
                row.Subcontract.Version
            })
            .ToListAsync(cancellationToken);

        return PagedResult<SubcontractListItemDto>.Of(items.Select(row => new SubcontractListItemDto(row.Id, row.Number, row.Title, row.SupplierId,
            row.FirmName, row.ProjectId, row.ProjectName, row.SubProjectName, row.Status.ToString(), row.VatTreatment.ToString(), row.RevisedSumMinor,
            row.CertifiedWorkMinor, Subcontract.PercentCertifiedBps(row.CertifiedWorkMinor, row.RevisedSumMinor),
            row.RetentionHeldMinor - row.RetentionReleasedMinor, row.PaidOnClaimsMinor + row.RetentionReleasedMinor,
            row.NetPayableMinor - row.PaidOnClaimsMinor, row.Version)).ToArray(), total, page);
    }

    public async Task<SubcontractDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var users = db.Set<User>();
        var head = await (
            from subcontract in db.Set<Subcontract>().AsNoTracking()
            where subcontract.Id == id
            join project in db.Set<Project>() on subcontract.ProjectId equals project.Id
            join firm in db.Set<Supplier>() on subcontract.SupplierId equals firm.Id
            select new
            {
                Subcontract = subcontract,
                ProjectName = project.Name,
                ProjectStatus = project.Status,
                FirmName = firm.Name,
                SubProjectName = db.Set<SubProject>().Where(sub => sub.Id == subcontract.SubProjectId).Select(sub => sub.Name).FirstOrDefault(),
                CreatedBy = users.Where(user => user.Id == subcontract.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                AwardedBy = users.Where(user => user.Id == subcontract.AwardedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                CompletedBy = users.Where(user => user.Id == subcontract.CompletedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                CancelledBy = users.Where(user => user.Id == subcontract.CancelledByUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).SingleOrDefaultAsync(cancellationToken);
        if (head is null) return null;

        var claims = await db.Set<SubcontractClaim>().AsNoTracking().Where(claim => claim.SubcontractId == id)
            .OrderByDescending(claim => claim.CreatedAtUtc).ThenByDescending(claim => claim.Id)
            .Select(claim => new
            {
                Claim = claim,
                CreatedBy = users.Where(user => user.Id == claim.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                CertifiedBy = users.Where(user => user.Id == claim.CertifiedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                VoidedBy = users.Where(user => user.Id == claim.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        var payments = await db.Set<SubcontractPayment>().AsNoTracking().Where(payment => payment.SubcontractId == id)
            .OrderByDescending(payment => payment.PaidOn).ThenByDescending(payment => payment.RecordedAtUtc)
            .Select(payment => new
            {
                Payment = payment,
                RecordedBy = users.Where(user => user.Id == payment.RecordedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                VoidedBy = users.Where(user => user.Id == payment.VoidedByUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        var variations = await db.Set<SubcontractVariation>().AsNoTracking().Where(variation => variation.SubcontractId == id)
            .OrderBy(variation => variation.Number)
            .Select(variation => new
            {
                Variation = variation,
                CreatedBy = users.Where(user => user.Id == variation.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        var s = head.Subcontract;
        var paidByClaim = payments.Where(row => row.Payment.IsRecorded && row.Payment.ClaimId is not null)
            .GroupBy(row => row.Payment.ClaimId!.Value).ToDictionary(group => group.Key, group => group.Sum(row => row.Payment.AmountMinor));
        var claimNumbers = claims.ToDictionary(row => row.Claim.Id, row => row.Claim.ClaimNumber);

        var claimDtos = claims.Select(row =>
        {
            var claim = row.Claim;
            var paid = paidByClaim.GetValueOrDefault(claim.Id);
            return new SubcontractClaimDto(claim.Id, claim.ClaimNumber, claim.Status.ToString(), claim.ClaimDate, claim.PeriodEnd, claim.CumulativeWorkMinor,
                claim.PreviousCumulativeMinor, claim.ThisClaimBaseMinor, claim.NetMinor, claim.VatMinor, claim.GrossMinor, claim.RetentionMinor,
                claim.WithholdingMinor, claim.NetPayableMinor, paid, claim.IsCertified ? claim.NetPayableMinor - paid : 0,
                claim.IsDraft && claim.PreviousCumulativeMinor != s.CertifiedWorkMinor, claim.Notes, claim.CostEntryId, row.CreatedBy, claim.CreatedAtUtc,
                row.CertifiedBy, claim.CertifiedAtUtc, row.VoidedBy, claim.VoidedAtUtc, claim.VoidReason);
        }).ToArray();

        var paymentDtos = payments.Select(row => new SubcontractPaymentDto(row.Payment.Id, row.Payment.Kind.ToString(), row.Payment.ClaimId,
            row.Payment.ClaimId is { } claimId ? claimNumbers.GetValueOrDefault(claimId) : null, row.Payment.AmountMinor, row.Payment.PaidOn,
            row.Payment.Method.ToString(), row.Payment.Reference, row.Payment.Note, row.Payment.Status.ToString(), row.RecordedBy, row.Payment.RecordedAtUtc,
            row.VoidedBy, row.Payment.VoidedAtUtc, row.Payment.VoidReason)).ToArray();

        var variationDtos = variations.Select(row => new SubcontractVariationDto(row.Variation.Id, row.Variation.Number, row.Variation.Description,
            row.Variation.AmountMinor, row.Variation.VariationDate, row.CreatedBy, row.Variation.CreatedAtUtc)).ToArray();

        return new SubcontractDto(s.Id, s.Number, s.Title, s.Notes, s.SupplierId, head.FirmName, s.ProjectId, head.ProjectName,
            head.ProjectStatus == ProjectStatus.Archived, s.SubProjectId, head.SubProjectName, s.Status.ToString(), s.VatTreatment.ToString(), s.VatRateBps,
            s.RetentionBps, s.WithholdingBps, s.ContractSumMinor, s.RevisedSumMinor - s.ContractSumMinor, s.RevisedSumMinor, s.RevisedNetMinor,
            s.RevisedVatMinor, s.RevisedGrossMinor, s.CertifiedWorkMinor, s.RevisedSumMinor - s.CertifiedWorkMinor,
            Subcontract.PercentCertifiedBps(s.CertifiedWorkMinor, s.RevisedSumMinor), s.CertifiedNetMinor, s.CertifiedGrossMinor, s.RetentionHeldMinor,
            s.RetentionReleasedMinor, s.RetentionHeldMinor - s.RetentionReleasedMinor, s.WithholdingMinor, s.NetPayableMinor, s.PaidOnClaimsMinor,
            s.NetPayableMinor - s.PaidOnClaimsMinor, head.CreatedBy, s.CreatedAtUtc, head.AwardedBy, s.AwardedAtUtc, head.CompletedBy, s.CompletedAtUtc,
            head.CancelledBy, s.CancelledAtUtc, s.CancelReason, claimDtos, paymentDtos, variationDtos, s.Version, s.WbsCode,
            WbsCatalog.FindByCode(s.WbsCode ?? "")?.SubScope, WbsCatalog.FindByCode(s.WbsCode ?? "")?.Division);
    }
}

/// <summary>
/// What awarded subcontracts still commit the projects to: for each active subcontract, the revised sum not yet certified (net and gross,
/// never below zero), added up per project in one grouped statement. Certified work is already actual cost, so it is never counted twice;
/// completed and cancelled subcontracts commit nothing more.
/// </summary>
internal sealed class SubcontractCommitmentSource(CmsDbContext db) : ICommitmentSource
{
    public async Task<IReadOnlyList<ProjectAmounts>> OpenCommitmentsAsync(Guid? projectId, bool includeArchived, CancellationToken cancellationToken)
    {
        var active = db.Set<Subcontract>().AsNoTracking().Where(item => item.Status == SubcontractStatus.Active);
        if (projectId is { } id) active = active.Where(item => item.ProjectId == id);
        else if (!includeArchived)
        {
            var visible = db.Set<Project>().Where(project => project.Status != ProjectStatus.Archived).Select(project => project.Id);
            active = active.Where(item => visible.Contains(item.ProjectId));
        }

        var rows = await active
            .GroupBy(item => item.ProjectId)
            .Select(group => new
            {
                ProjectId = group.Key,
                // Once all the work is certified nothing is committed: each claim rounds its own VAT, so net and gross can differ from the
                // revised sum's by a centavo or two, and that difference is not a commitment.
                Net = group.Sum(item => item.CertifiedWorkMinor >= item.RevisedSumMinor ? 0L
                    : item.RevisedNetMinor > item.CertifiedNetMinor ? item.RevisedNetMinor - item.CertifiedNetMinor : 0L),
                Gross = group.Sum(item => item.CertifiedWorkMinor >= item.RevisedSumMinor ? 0L
                    : item.RevisedGrossMinor > item.CertifiedGrossMinor ? item.RevisedGrossMinor - item.CertifiedGrossMinor : 0L)
            })
            .ToListAsync(cancellationToken);
        return rows.Where(row => row.Net > 0 || row.Gross > 0).Select(row => new ProjectAmounts(row.ProjectId, row.Net, row.Gross)).ToArray();
    }
}
