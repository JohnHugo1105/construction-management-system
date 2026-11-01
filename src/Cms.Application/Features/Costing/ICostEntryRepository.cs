using Cms.Domain.Features.Costing;

namespace Cms.Application.Features.Costing;

/// <summary>
/// The part of the cost-entry store that other features use to post and reverse the costs they cause: approved payroll and charged
/// stock transfers. The Costing feature implements this and may add members for its own use.
/// </summary>
public interface ICostEntryRepository
{
    Task<CostEntry?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(CostEntry entry);

    /// <summary>The cost entries a system action made (a payroll run or a stock movement), voided ones included.</summary>
    Task<IReadOnlyList<CostEntry>> ListByOriginAsync(CostOrigin origin, Guid originReferenceId, CancellationToken cancellationToken);
}
