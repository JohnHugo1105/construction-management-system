namespace ConstructOPX.Domain.Common;

/// <summary>The record does not exist, or belongs to another organization (the two are indistinguishable on purpose).</summary>
public sealed class NotFoundException(string what) : Exception($"We couldn't find {what}. It may have been removed.");
