using ConstructOPX.Domain.Features.Costing;

namespace ConstructOPX.Application.Features.Costing;

/// <summary>
/// The cost-entry store. <see cref="GetAsync"/>, <see cref="Add"/> and <see cref="ListByOriginAsync"/> are what other features use to post and
/// reverse the costs they cause (approved payroll, charged stock transfers); their signatures must not change. The members below them
/// belong to the Costing feature's own use cases.
/// </summary>
public interface ICostEntryRepository
{
    Task<CostEntry?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(CostEntry entry);

    /// <summary>The cost entries a system action made (a payroll run or a stock movement), voided ones included.</summary>
    Task<IReadOnlyList<CostEntry>> ListByOriginAsync(CostOrigin origin, Guid originReferenceId, CancellationToken cancellationToken);

    /// <summary>The entry an earlier request with this id created, so a repeated request returns it instead of making a second one.</summary>
    Task<CostEntry?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// A posted, manually recorded entry of the project with the same supplier and invoice number (both already normalized), other than
    /// <paramref name="excludeEntryId"/>. Voided entries and system-made costs never count. Null when the invoice is not recorded yet.
    /// </summary>
    Task<DuplicateInvoice?> FindDuplicateInvoiceAsync(Guid projectId, string normalizedSourceDealer, string normalizedInvoiceNumber,
        Guid? excludeEntryId, CancellationToken cancellationToken);
}

/// <summary>The earlier entry that already holds a supplier invoice.</summary>
public sealed record DuplicateInvoice(Guid EntryId, DateOnly EntryDate);
