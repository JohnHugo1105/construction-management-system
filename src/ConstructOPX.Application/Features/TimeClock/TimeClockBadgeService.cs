using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Workforce;

namespace ConstructOPX.Application.Features.TimeClock;

/// <summary>
/// Badge codes and badge cards (HR). A code is random, unique in the company and never derived from anything about the worker; reissuing replaces it
/// and the old badge stops working at once. Audit lines say a code was issued, never the code.
/// </summary>
public sealed class TimeClockBadgeService(IWorkerRepository workers, ITimePunchRepository punches, ITimeClockReader reader,
    IOrganizationRepository organizations, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public const int MaximumBadges = 100;

    public async Task<PunchCodeIssuedDto> IssueAsync(Guid workerId, IssuePunchCodeRequest request, CancellationToken cancellationToken)
    {
        var worker = await workers.GetAsync(workerId, includeHistory: false, cancellationToken) ?? throw new NotFoundException("that worker");
        ConcurrencyGuard.Ensure(worker.Version, request.Version);
        var code = (await NewCodesAsync(1, cancellationToken))[0];
        var reissued = worker.IssuePunchCode(code, clock.UtcNow);
        audit.Record(reissued ? "Worker.PunchCodeReissued" : "Worker.PunchCodeIssued", nameof(Worker), worker.Id,
            reissued
                ? $"Reissued the time clock badge code of {worker.FullName}; the old badge stopped working."
                : $"Issued a time clock badge code to {worker.FullName}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PunchCodeIssuedDto(worker.Id, code, reissued, worker.Version);
    }

    /// <summary>Gives a code to every active worker without one, at most 100 at a time (the rest wait for the next call).</summary>
    public async Task<IssueMissingCodesResultDto> IssueMissingAsync(CancellationToken cancellationToken)
    {
        var (count, ids) = await reader.ActiveWithoutCodeAsync(MaximumBadges, cancellationToken);
        if (ids.Count == 0) return new IssueMissingCodesResultDto(0, [], 0);

        var found = await workers.GetManyAsync(ids, includeHistory: false, cancellationToken);
        var codes = await NewCodesAsync(found.Count, cancellationToken);
        var now = clock.UtcNow;
        var issued = new List<Guid>(found.Count);
        for (var index = 0; index < found.Count; index++)
        {
            var worker = found[index];
            if (worker.PunchCode is not null || worker.Status != WorkerStatus.Active) continue;
            worker.IssuePunchCode(codes[index], now);
            audit.Record("Worker.PunchCodeIssued", nameof(Worker), worker.Id, $"Issued a time clock badge code to {worker.FullName}.");
            issued.Add(worker.Id);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new IssueMissingCodesResultDto(issued.Count, ids.Where(issued.Contains).ToArray(), Math.Max(0, count - issued.Count));
    }

    /// <summary>Badge cards for these workers (at most 100). A worker of another company, or one that doesn't exist, is simply not there.</summary>
    public async Task<BadgeSheetDto> BadgesAsync(IReadOnlyCollection<Guid> workerIds, CancellationToken cancellationToken)
    {
        var ids = workerIds.Distinct().ToArray();
        if (ids.Length > MaximumBadges) throw new ValidationFailedException("workerIds", $"Print at most {MaximumBadges} badges at a time.");
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var items = ids.Length == 0 ? [] : await reader.BadgesAsync(ids, organization.TodayLocal(clock.UtcNow), cancellationToken);
        var (withoutCode, _) = await reader.ActiveWithoutCodeAsync(0, cancellationToken);
        return new BadgeSheetDto(organization.Name, items, withoutCode);
    }

    /// <summary>New codes no worker of the company carries (a clash is about one in 10^17, but it is checked).</summary>
    private async Task<IReadOnlyList<string>> NewCodesAsync(int count, CancellationToken cancellationToken)
    {
        var codes = new List<string>(count);
        for (var attempt = 0; codes.Count < count; attempt++)
        {
            if (attempt == 5) throw new InvalidOperationException("Could not find unused badge codes.");
            var candidates = Enumerable.Range(0, count - codes.Count).Select(_ => PunchCodes.New()).Distinct().Except(codes).ToArray();
            var inUse = await punches.CodesInUseAsync(candidates, cancellationToken);
            codes.AddRange(candidates.Where(code => !inUse.Contains(code)));
        }
        return codes;
    }
}
