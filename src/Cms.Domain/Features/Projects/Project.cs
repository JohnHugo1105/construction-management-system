using Cms.Domain.Common;

namespace Cms.Domain.Features.Projects;

/// <summary>
/// A construction project and its sub-projects. A project is never deleted: once budget lines, costs, workers or stock refer to
/// it, deleting would orphan them (the retired desktop app's "remove" did exactly that). It is archived with a reason, stays
/// readable, and can be restored.
/// </summary>
public sealed class Project : AggregateRoot
{
    private readonly List<SubProject> _subProjects = [];

    private Project() { }

    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";
    public string? Client { get; private set; }
    public string? Location { get; private set; }

    /// <summary>The agreed contract price including VAT, in minor units. The desktop app held it only as unsaved free text.</summary>
    public long? ContractPriceMinor { get; private set; }

    public DateOnly StartDate { get; private set; }
    public DateOnly? TargetEndDate { get; private set; }
    public ProjectStatus Status { get; private set; } = ProjectStatus.Planning;
    public ProjectStatus? StatusBeforeArchive { get; private set; }
    public ArchiveReason? ArchiveReason { get; private set; }
    public string? ArchiveNote { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public IReadOnlyList<SubProject> SubProjects => _subProjects;

    public bool IsArchived => Status == ProjectStatus.Archived;

    public static Project Create(Guid id, Guid tenantId, string name, string? client, string? location, long? contractPriceMinor,
        DateOnly startDate, DateOnly? targetEndDate, DateTimeOffset now)
    {
        var project = new Project();
        project.Initialize(id, tenantId, now);
        project.ApplyDetails(name, client, location, contractPriceMinor, startDate, targetEndDate);
        return project;
    }

    public void UpdateDetails(string name, string? client, string? location, long? contractPriceMinor, DateOnly startDate,
        DateOnly? targetEndDate, DateTimeOffset now)
    {
        EnsureNotArchived();
        ApplyDetails(name, client, location, contractPriceMinor, startDate, targetEndDate);
        Touch(now);
    }

    private void ApplyDetails(string name, string? client, string? location, long? contractPriceMinor, DateOnly startDate,
        DateOnly? targetEndDate)
    {
        var errors = new ValidationErrors();
        string? cleanName = null;
        try { cleanName = Guard.RequiredText(name, "name", "the project name", 120); }
        catch (ValidationFailedException ex) { errors.Add("name", ex.Errors["name"][0]); }
        if (contractPriceMinor is < 0) errors.Add("contractPriceMinor", "The contract price can't be negative.");
        else if (contractPriceMinor is { } price) MoneyLimits.CheckAmount(errors, price, "contractPriceMinor", "the contract price");
        if (startDate == default) errors.Add("startDate", "Choose the date the project started.");
        if (targetEndDate is { } end && startDate != default && end < startDate) errors.Add("targetEndDate", "The target end date can't be before the start date.");
        errors.ThrowIfAny();

        Name = cleanName!;
        NormalizedName = Guard.Normalize(cleanName!);
        Client = Guard.OptionalText(client, "client", "the client name", 120);
        Location = Guard.OptionalText(location, "location", "the location", 200);
        ContractPriceMinor = contractPriceMinor;
        StartDate = startDate;
        TargetEndDate = targetEndDate;
    }

    public void Start(DateTimeOffset now) => Transition(ProjectStatus.Active, [ProjectStatus.Planning], "Only a project that is still being planned can be started.", now);

    public void PutOnHold(DateTimeOffset now) => Transition(ProjectStatus.OnHold, [ProjectStatus.Active], "Only an active project can be put on hold.", now);

    public void Resume(DateTimeOffset now) => Transition(ProjectStatus.Active, [ProjectStatus.OnHold], "Only a project on hold can be resumed.", now);

    public void Complete(DateTimeOffset now) =>
        Transition(ProjectStatus.Completed, [ProjectStatus.Active, ProjectStatus.OnHold], "Only an active project, or one on hold, can be marked completed.", now);

    public void Reopen(DateTimeOffset now) => Transition(ProjectStatus.Active, [ProjectStatus.Completed], "Only a completed project can be reopened.", now);

    public void Archive(ArchiveReason reason, string? note, DateTimeOffset now)
    {
        if (IsArchived) throw new DomainRuleException("This project is already archived.");
        if (!Enum.IsDefined(reason)) throw new ValidationFailedException("reason", "Choose why the project is being archived.");
        var cleanNote = Guard.OptionalText(note, "note", "the note", 500);
        if (reason == Features.Projects.ArchiveReason.Other && cleanNote is null)
            throw new ValidationFailedException("note", "Say why the project is being archived.");

        StatusBeforeArchive = Status;
        Status = ProjectStatus.Archived;
        ArchiveReason = reason;
        ArchiveNote = cleanNote;
        ArchivedAtUtc = now;
        foreach (var sub in _subProjects) sub.ArchiveWithProject();
        Touch(now);
    }

    /// <summary>Brings an archived project back to the state it was in. The desktop app had no way to restore.</summary>
    public void Restore(DateTimeOffset now)
    {
        if (!IsArchived) throw new DomainRuleException("Only an archived project can be restored.");
        Status = StatusBeforeArchive ?? ProjectStatus.Active;
        StatusBeforeArchive = null;
        ArchiveReason = null;
        ArchiveNote = null;
        ArchivedAtUtc = null;
        foreach (var sub in _subProjects) sub.RestoreWithProject();
        Touch(now);
    }

    public SubProject AddSubProject(Guid id, string name, DateOnly startDate, DateTimeOffset now)
    {
        EnsureNotArchived();
        var cleanName = Guard.RequiredText(name, "name", "the sub-project name", 120);
        if (startDate == default) throw new ValidationFailedException("startDate", "Choose the date the sub-project started.");
        var normalized = Guard.Normalize(cleanName);
        if (_subProjects.Any(sub => sub.NormalizedName == normalized))
            throw new DomainRuleException($"This project already has a sub-project named \"{cleanName}\".");

        var sub = new SubProject(id, TenantId, Id, cleanName, startDate);
        _subProjects.Add(sub);
        Touch(now);
        return sub;
    }

    public void UpdateSubProject(Guid subProjectId, string name, DateOnly startDate, DateTimeOffset now)
    {
        EnsureNotArchived();
        var sub = FindSubProject(subProjectId);
        var cleanName = Guard.RequiredText(name, "name", "the sub-project name", 120);
        if (startDate == default) throw new ValidationFailedException("startDate", "Choose the date the sub-project started.");
        var normalized = Guard.Normalize(cleanName);
        if (_subProjects.Any(other => other.Id != subProjectId && other.NormalizedName == normalized))
            throw new DomainRuleException($"This project already has a sub-project named \"{cleanName}\".");
        sub.Rename(cleanName, startDate);
        Touch(now);
    }

    public void ArchiveSubProject(Guid subProjectId, DateTimeOffset now)
    {
        EnsureNotArchived();
        FindSubProject(subProjectId).Archive();
        Touch(now);
    }

    public void RestoreSubProject(Guid subProjectId, DateTimeOffset now)
    {
        EnsureNotArchived();
        FindSubProject(subProjectId).Restore();
        Touch(now);
    }

    /// <summary>Guards every change that adds to the project: budget lines, costs, assignments, attendance, stock movements.</summary>
    public void EnsureNotArchived()
    {
        if (IsArchived) throw new DomainRuleException("This project is archived. Restore it before making changes.");
    }

    /// <summary>A sub-project must belong to this project and still be active to receive new records.</summary>
    public void EnsureCanReceiveRecordsFor(Guid? subProjectId)
    {
        EnsureNotArchived();
        if (subProjectId is null) return;
        var sub = _subProjects.FirstOrDefault(item => item.Id == subProjectId) ??
            throw new ValidationFailedException("subProjectId", "That sub-project doesn't belong to this project.");
        if (sub.Status == SubProjectStatus.Archived) throw new DomainRuleException("That sub-project is archived. Restore it first.");
    }

    private SubProject FindSubProject(Guid subProjectId) =>
        _subProjects.FirstOrDefault(sub => sub.Id == subProjectId) ?? throw new NotFoundException("that sub-project");

    private void Transition(ProjectStatus target, ProjectStatus[] allowedFrom, string message, DateTimeOffset now)
    {
        if (!allowedFrom.Contains(Status)) throw new DomainRuleException(message);
        Status = target;
        Touch(now);
    }
}
