using Cms.Application.Common;
using Cms.Domain.Features.Workforce;

namespace Cms.Application.Features.Workforce;

public interface IWorkerRepository
{
    /// <summary>
    /// Loads a worker tracked for change. <paramref name="includeHistory"/> adds the pay history and postings, which pay and
    /// assignment rules need; attendance and profile edits don't. Null when the worker isn't in the organization.
    /// </summary>
    Task<Worker?> GetAsync(Guid id, bool includeHistory, CancellationToken cancellationToken);

    /// <summary>Loads several workers tracked for change. An id that doesn't exist in the organization is simply absent.</summary>
    Task<IReadOnlyList<Worker>> GetManyAsync(IReadOnlyCollection<Guid> ids, bool includeHistory, CancellationToken cancellationToken);

    Task<bool> EmployeeNumberExistsAsync(string normalizedEmployeeNumber, Guid? excludeWorkerId, CancellationToken cancellationToken);

    /// <summary>The worker a form submission already hired, so a double click returns that worker instead of a second one.</summary>
    Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    /// <summary>The latest work day of this worker that a payroll run has already taken (draft or approved), or null when none has.</summary>
    Task<DateOnly?> LatestWorkDateInPayrollAsync(Guid workerId, CancellationToken cancellationToken);

    void Add(Worker worker);
}

/// <summary>
/// Reads workers straight into DTOs in SQL. Pay is only returned when <c>includePay</c> is true, which the service sets from the
/// caller's permission, so a pay figure can't reach a person who isn't allowed to see it.
/// </summary>
public interface IWorkerReader
{
    Task<PagedResult<WorkerListItemDto>> ListAsync(WorkerListQuery query, DateOnly today, PageRequest page, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkerLookupDto>> LookupAsync(string? search, bool includeLeft, CancellationToken cancellationToken);

    Task<WorkerDetailDto?> GetAsync(Guid id, bool includePay, DateOnly today, CancellationToken cancellationToken);
}

public sealed record WorkerListQuery(string? Search, string? Status, Guid? ProjectId);

public sealed record WorkerListItemDto(Guid Id, string EmployeeNumber, string FullName, string Status, string? Designation, Guid? CurrentProjectId,
    string? CurrentProjectName, DateOnly DateHired, int Version);

public sealed record WorkerLookupDto(Guid Id, string FullName, string EmployeeNumber, string Status);

/// <summary>One line of pay history. Only ever returned to someone who may see pay.</summary>
public sealed record WorkerRateDto(Guid Id, string Designation, long RateMinor, string RateUnit, string Category, DateOnly EffectiveFrom,
    string? Reason, DateTimeOffset RecordedAtUtc);

public sealed record WorkerAssignmentDto(Guid Id, Guid ProjectId, string ProjectName, Guid? SubProjectId, string? SubProjectName,
    DateOnly StartDate, DateOnly? EndDate, bool IsCurrent);

public sealed record WorkerCaseDto(Guid Id, DateOnly CaseDate, string Title, string? Remarks, Guid? ProjectId, string? ProjectName,
    DateTimeOffset RecordedAtUtc);

/// <summary>
/// A worker's profile. <c>CurrentRateMinor</c>, <c>CurrentRateUnit</c> and <c>Rates</c> are null, null and empty unless the caller may
/// see pay (the payroll.read permission).
/// </summary>
public sealed record WorkerDetailDto(Guid Id, string EmployeeNumber, string FirstName, string? MiddleName, string LastName, string FullName,
    DateOnly? DateOfBirth, DateOnly DateHired, string? MaritalStatus, string? SpouseName, string? Religion, string? EducationalAttainment,
    string? PresentAddress, string? ProvincialAddress, string? ContactNumber, string? Reference, string Status, DateOnly? StatusChangedOn,
    string? StatusReason, string? Designation, long? CurrentRateMinor, string? CurrentRateUnit, WorkerAssignmentDto? CurrentAssignment,
    IReadOnlyList<WorkerAssignmentDto> Assignments, IReadOnlyList<WorkerRateDto> Rates, IReadOnlyList<WorkerCaseDto> Cases, int Version);

public sealed record HireWorkerRequest(string? EmployeeNumber, string? FirstName, string? MiddleName, string? LastName, DateOnly DateHired,
    string? Designation, long RateMinor, string? RateUnit, string? ClientRequestId);

public sealed record UpdateWorkerRequest(string? FirstName, string? MiddleName, string? LastName, DateOnly? DateOfBirth, string? MaritalStatus,
    string? SpouseName, string? Religion, string? EducationalAttainment, string? PresentAddress, string? ProvincialAddress, string? ContactNumber,
    string? Reference, int Version);

/// <summary><c>Category</c> is Promotion, Demotion or RateAdjustment; <c>RateUnit</c> is Daily or Hourly.</summary>
public sealed record ChangeWorkerRateRequest(string? Category, string? Designation, long RateMinor, string? RateUnit, DateOnly EffectiveFrom,
    string? Reason, int Version);

public sealed record AssignWorkerRequest(Guid ProjectId, Guid? SubProjectId, DateOnly StartDate, int Version);

/// <param name="Versions">
/// Optional: the version of each worker as the person last saw it (worker id to version). A worker whose record changed since is refused
/// like any stale edit, and the person reloads. Left out, the crew is posted on the current records.
/// </param>
public sealed record BulkAssignWorkersRequest(IReadOnlyList<Guid>? WorkerIds, Guid ProjectId, Guid? SubProjectId, DateOnly StartDate,
    IReadOnlyDictionary<Guid, int>? Versions = null);

public sealed record ReleaseWorkerRequest(DateOnly EndDate, int Version);

public sealed record ChangeWorkerStatusRequest(string? Status, DateOnly EffectiveDate, string? Reason, int Version);

public sealed record ReinstateWorkerRequest(DateOnly EffectiveDate, string? Reason, int Version);

public sealed record RecordWorkerCaseRequest(string? Title, string? Remarks, DateOnly CaseDate, Guid? ProjectId, int Version);

/// <summary>Turns request text into the enums the domain checks. Text that names no value becomes an undefined value, so the domain reports it with the other problems.</summary>
internal static class EnumText
{
    public static T Parse<T>(string? text) where T : struct, Enum
    {
        var name = text?.Trim();
        foreach (var candidate in Enum.GetValues<T>())
            if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase)) return candidate;
        return (T)Enum.ToObject(typeof(T), -1);
    }
}

public static class WorkerNames
{
    /// <summary>The same wording as <see cref="Worker.FullName"/>, for lists that read the name columns without loading a worker.</summary>
    public static string Full(string firstName, string? middleName, string lastName) =>
        string.Join(' ', new[] { firstName, middleName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
