namespace Cms.Domain.Common;

/// <summary>Someone else changed the record after it was opened. The person is asked to reload and try again; nothing is overwritten.</summary>
public sealed class ConcurrencyConflictException()
    : Exception("Someone else changed this record while you were editing. Reload it to see the latest version, then try again.");
