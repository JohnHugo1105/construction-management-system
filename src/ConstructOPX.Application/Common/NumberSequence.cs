namespace ConstructOPX.Application.Common;

/// <summary>
/// Hands out the next number of a named sequence for the signed-in company (purchase order 1, 2, 3...). Two people asking at the same
/// moment never get the same number. A number is taken before the record is saved, so a save that then fails leaves a gap; numbers are
/// unique and rising, not guaranteed unbroken.
/// </summary>
public interface INumberSequence
{
    /// <param name="key">The sequence, for example <c>purchase-order</c>, or <c>change-order:{projectId}</c> for one that restarts per project.</param>
    Task<long> NextAsync(string key, CancellationToken cancellationToken);
}
