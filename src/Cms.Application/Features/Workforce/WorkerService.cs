using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Domain.Common;
using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;

namespace Cms.Application.Features.Workforce;

/// <summary>
/// Worker use cases: hiring, profile, pay history, postings, status and cases. Pay is confidential: it is returned only to people who
/// hold the payroll.read permission, and no audit line or log ever carries a pay figure.
/// </summary>
public sealed class WorkerService(IWorkerRepository workers, IWorkerReader reader, IProjectRepository projects,
    IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    private const int MaximumBulkWorkers = 200;

    private bool CanSeePay => Permissions.IsAllowed(currentUser.Roles, Permissions.PayrollRead);

    public async Task<PagedResult<WorkerListItemDto>> ListAsync(WorkerListQuery query, PageRequest page, CancellationToken cancellationToken) =>
        await reader.ListAsync(query, await TodayAsync(cancellationToken), page, cancellationToken);

    public Task<IReadOnlyList<WorkerLookupDto>> LookupAsync(string? search, bool includeLeft, CancellationToken cancellationToken) =>
        reader.LookupAsync(search, includeLeft, cancellationToken);

    public async Task<WorkerDetailDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ReadAsync(id, await TodayAsync(cancellationToken), cancellationToken);

    public async Task<WorkerDetailDto> HireAsync(HireWorkerRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var requestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId.Trim();
        if (requestId is not null && await workers.FindIdByClientRequestAsync(requestId, cancellationToken) is { } already)
            return await ReadAsync(already, today, cancellationToken);

        try
        {
            return await HireNewAsync(request, requestId, today, cancellationToken);
        }
        catch (Exception ex) when (requestId is not null && ex is DomainRuleException or ValidationFailedException)
        {
            // The same form submitted twice at once: the first submission can be saved after this one looked for it and before this one
            // finished (so the employee number, or the unique request id at the database, now refuses it). Answer it with the worker the
            // first one hired. Without such a worker the refusal is a real one and is reported as it is.
            if (await workers.FindIdByClientRequestAsync(requestId, cancellationToken) is { } original) return await ReadAsync(original, today, cancellationToken);
            throw;
        }
    }

    private async Task<WorkerDetailDto> HireNewAsync(HireWorkerRequest request, string? requestId, DateOnly today, CancellationToken cancellationToken)
    {
        var worker = Worker.Hire(Guid.NewGuid(), currentUser.TenantId, request.EmployeeNumber, request.FirstName, request.MiddleName, request.LastName,
            request.DateHired, request.Designation, request.RateMinor, EnumText.Parse<RateUnit>(request.RateUnit), currentUser.UserId, today,
            clock.UtcNow, requestId);
        if (await workers.EmployeeNumberExistsAsync(worker.NormalizedEmployeeNumber, null, cancellationToken))
            throw new ValidationFailedException("employeeNumber", $"Another worker already has the employee number {worker.EmployeeNumber}. Use a different number.");

        workers.Add(worker);
        audit.Record("Worker.Hired", nameof(Worker), worker.Id, $"Hired {worker.FullName} ({worker.EmployeeNumber}) as {worker.CurrentRate.Designation}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(worker.Id, today, cancellationToken);
    }

    public async Task<WorkerDetailDto> UpdateAsync(Guid id, UpdateWorkerRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var worker = await LoadAsync(id, includeHistory: false, cancellationToken);
        ConcurrencyGuard.Ensure(worker.Version, request.Version);
        worker.UpdateProfile(request.FirstName, request.MiddleName, request.LastName, request.DateOfBirth, request.MaritalStatus, request.SpouseName,
            request.Religion, request.EducationalAttainment, request.PresentAddress, request.ProvincialAddress, request.ContactNumber, request.Reference,
            today, clock.UtcNow);

        audit.Record("Worker.ProfileUpdated", nameof(Worker), worker.Id, $"Updated the profile of {worker.FullName}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(worker.Id, today, cancellationToken);
    }

    public async Task<WorkerDetailDto> ChangeRateAsync(Guid id, ChangeWorkerRateRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var worker = await LoadAsync(id, includeHistory: true, cancellationToken);
        ConcurrencyGuard.Ensure(worker.Version, request.Version);
        var category = EnumText.Parse<RateChangeCategory>(request.Category);
        var previousDesignation = worker.CurrentRate.Designation;
        worker.ChangeRate(category, request.Designation, request.RateMinor, EnumText.Parse<RateUnit>(request.RateUnit), request.EffectiveFrom, request.Reason,
            currentUser.UserId, clock.UtcNow);

        // The audit trail is read by more people than payroll is, so it says that pay changed and never what it changed to. The reason is
        // free text a person typed and may hold an amount, so it stays on the pay history and out of the audit line.
        var designation = worker.CurrentRate.Designation;
        var what = category switch
        {
            RateChangeCategory.Promotion => $"promotion from {previousDesignation} to {designation}",
            RateChangeCategory.Demotion => $"demotion from {previousDesignation} to {designation}",
            _ => $"rate adjustment, {designation}"
        };
        audit.Record("Worker.RateChanged", nameof(Worker), worker.Id,
            $"Changed pay rate for {worker.FullName} ({what}) effective {Format(request.EffectiveFrom)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(worker.Id, today, cancellationToken);
    }

    public async Task<WorkerDetailDto> AssignAsync(Guid id, AssignWorkerRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var worker = await LoadAsync(id, includeHistory: true, cancellationToken);
        ConcurrencyGuard.Ensure(worker.Version, request.Version);
        var project = await LoadProjectAsync(request.ProjectId, cancellationToken);

        worker.AssignTo(project, request.SubProjectId, request.StartDate, currentUser.UserId, clock.UtcNow);
        audit.Record("Worker.Assigned", nameof(Worker), worker.Id, AssignedSummary(worker, project, request.SubProjectId, request.StartDate));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(worker.Id, today, cancellationToken);
    }

    /// <summary>
    /// Posts a crew to one project in one step. All or nothing: if any worker can't be posted, nobody is, and the answer lists each
    /// worker who couldn't be and why.
    /// </summary>
    public async Task<IReadOnlyList<WorkerListItemDto>> AssignManyAsync(BulkAssignWorkersRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var ids = (request.WorkerIds ?? []).Distinct().ToArray();
        if (ids.Length == 0) throw new ValidationFailedException("workerIds", "Choose at least one worker.");
        if (ids.Length > MaximumBulkWorkers) throw new ValidationFailedException("workerIds", $"Assign at most {MaximumBulkWorkers} workers at a time.");

        var project = await LoadProjectAsync(request.ProjectId, cancellationToken);
        // A project or sub-project that can't take records fails for everyone: say so once instead of once per worker.
        project.EnsureCanReceiveRecordsFor(request.SubProjectId);

        var found = (await workers.GetManyAsync(ids, includeHistory: true, cancellationToken)).ToDictionary(worker => worker.Id);
        var problems = new List<string>();
        var now = clock.UtcNow;
        foreach (var id in ids)
        {
            if (!found.TryGetValue(id, out var worker))
            {
                problems.Add("One of the chosen workers couldn't be found. Reload the list and try again.");
                continue;
            }

            try { worker.AssignTo(project, request.SubProjectId, request.StartDate, currentUser.UserId, now); }
            catch (Exception ex) when (ex is DomainRuleException or ValidationFailedException)
            {
                var reason = ex is ValidationFailedException validation ? string.Join(' ', validation.Errors.Values.SelectMany(messages => messages)) : ex.Message;
                problems.Add(reason.StartsWith(worker.FullName, StringComparison.Ordinal) ? reason : $"{worker.FullName}: {reason}");
            }
        }

        if (problems.Count > 0)
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["workers"] = problems.ToArray() });

        foreach (var worker in found.Values)
            audit.Record("Worker.Assigned", nameof(Worker), worker.Id, AssignedSummary(worker, project, request.SubProjectId, request.StartDate));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var assigned = new List<WorkerListItemDto>(ids.Length);
        foreach (var worker in found.Values.OrderBy(item => item.LastName).ThenBy(item => item.FirstName))
            assigned.Add(new WorkerListItemDto(worker.Id, worker.EmployeeNumber, worker.FullName, worker.Status.ToString(),
                worker.RateOn(today)?.Designation, project.Id, project.Name, worker.DateHired, worker.Version));
        return assigned;
    }

    public async Task<WorkerDetailDto> ReleaseAsync(Guid id, ReleaseWorkerRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var worker = await LoadAsync(id, includeHistory: true, cancellationToken);
        ConcurrencyGuard.Ensure(worker.Version, request.Version);

        worker.Release(request.EndDate, clock.UtcNow);
        audit.Record("Worker.Released", nameof(Worker), worker.Id, $"Released {worker.FullName} from their project on {Format(request.EndDate)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(worker.Id, today, cancellationToken);
    }

    public async Task<WorkerDetailDto> ChangeStatusAsync(Guid id, ChangeWorkerStatusRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var worker = await LoadAsync(id, includeHistory: true, cancellationToken);
        ConcurrencyGuard.Ensure(worker.Version, request.Version);

        worker.ChangeStatus(EnumText.Parse<WorkerStatus>(request.Status), request.EffectiveDate, request.Reason, today, clock.UtcNow);
        audit.Record("Worker.StatusChanged", nameof(Worker), worker.Id,
            $"Changed the status of {worker.FullName} to {WorkerStatusRules.Label(worker.Status).ToLowerInvariant()} effective {Format(request.EffectiveDate)}" +
            (worker.StatusReason is null ? "." : $": {worker.StatusReason}"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(worker.Id, today, cancellationToken);
    }

    public async Task<WorkerDetailDto> ReinstateAsync(Guid id, ReinstateWorkerRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var worker = await LoadAsync(id, includeHistory: false, cancellationToken);
        ConcurrencyGuard.Ensure(worker.Version, request.Version);

        worker.Reinstate(request.EffectiveDate, request.Reason, today, clock.UtcNow);
        audit.Record("Worker.Reinstated", nameof(Worker), worker.Id, $"Reinstated {worker.FullName} effective {Format(request.EffectiveDate)}: {worker.StatusReason}");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(worker.Id, today, cancellationToken);
    }

    public async Task<WorkerDetailDto> RecordCaseAsync(Guid id, RecordWorkerCaseRequest request, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var worker = await LoadAsync(id, includeHistory: false, cancellationToken);
        ConcurrencyGuard.Ensure(worker.Version, request.Version);
        if (request.ProjectId is { } projectId) await LoadProjectAsync(projectId, cancellationToken);

        var workerCase = worker.RecordCase(request.Title, request.Remarks, request.CaseDate, request.ProjectId, currentUser.UserId, today, clock.UtcNow);
        audit.Record("Worker.CaseRecorded", nameof(Worker), worker.Id, $"Recorded a case for {worker.FullName}: {workerCase.Title}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(worker.Id, today, cancellationToken);
    }

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        (await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow);

    private async Task<WorkerDetailDto> ReadAsync(Guid id, DateOnly today, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, CanSeePay, today, cancellationToken) ?? throw new NotFoundException("that worker");

    private async Task<Worker> LoadAsync(Guid id, bool includeHistory, CancellationToken cancellationToken) =>
        await workers.GetAsync(id, includeHistory, cancellationToken) ?? throw new NotFoundException("that worker");

    private async Task<Project> LoadProjectAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");

    private static string AssignedSummary(Worker worker, Project project, Guid? subProjectId, DateOnly startDate)
    {
        var sub = subProjectId is null ? null : project.SubProjects.FirstOrDefault(item => item.Id == subProjectId)?.Name;
        return $"Assigned {worker.FullName} to {project.Name}{(sub is null ? "" : $" ({sub})")} from {Format(startDate)}.";
    }

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
