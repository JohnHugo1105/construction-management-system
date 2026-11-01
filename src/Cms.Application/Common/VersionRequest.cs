namespace Cms.Application.Common;

/// <summary>The body of an action that only needs the version the person last saw (start, archive, approve, void without a reason).</summary>
public sealed record VersionRequest(int Version);
