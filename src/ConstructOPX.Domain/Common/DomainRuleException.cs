namespace ConstructOPX.Domain.Common;

/// <summary>
/// A business rule forbids the requested change in the record's current state (for example editing an archived project).
/// The message is written for the end user and is shown as is, so it names what happened and what to do next.
/// </summary>
public sealed class DomainRuleException(string message) : Exception(message);
