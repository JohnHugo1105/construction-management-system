using ConstructOPX.Domain.Common;

namespace ConstructOPX.Domain.Features.Projects;

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

    /// <summary>When the budget was frozen as the baseline. Null while the budget is open to direct editing.</summary>
    public DateTimeOffset? BudgetBaselineLockedAtUtc { get; private set; }

    public Guid? BudgetBaselineLockedByUserId { get; private set; }

    /// <summary>The sum of the active budget lines when the budget was locked, before VAT. Null while unlocked.</summary>
    public long? BaselineBudgetNetMinor { get; private set; }

    /// <summary>The sum of the active budget lines when the budget was locked, with VAT. Null while unlocked.</summary>
    public long? BaselineBudgetGrossMinor { get; private set; }

    /// <summary>The contract price when the budget was locked (null when the project had none). Meaningful only while locked.</summary>
    public long? BaselineContractPriceMinor { get; private set; }

    /// <summary>Why the budget was last unlocked, kept on the record (the activity log never holds typed reasons).</summary>
    public string? BudgetBaselineUnlockReason { get; private set; }

    public DateTimeOffset? BudgetBaselineUnlockedAtUtc { get; private set; }

    public IReadOnlyList<SubProject> SubProjects => _subProjects;

    public bool IsArchived => Status == ProjectStatus.Archived;

    /// <summary>The budget is frozen: it changes only through approved change orders.</summary>
    public bool IsBudgetLocked => BudgetBaselineLockedAtUtc is not null;

    public const string BudgetLockedMessage = "The budget is locked. Record a change order to change it.";

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
        // A locked budget freezes the contract price with it: the revised price is the baseline plus approved change orders, so a price
        // typed over here would make the two disagree. A project locked before it had a price may still record its first one.
        var firstPriceOnLockedBudget = IsBudgetLocked && ContractPriceMinor is null && BaselineContractPriceMinor is null;
        if (IsBudgetLocked && contractPriceMinor != ContractPriceMinor && !firstPriceOnLockedBudget)
            errors.Add("contractPriceMinor", "The contract price is locked with the budget. Record a change order to change it.");
        errors.ThrowIfAny();
        if (firstPriceOnLockedBudget && contractPriceMinor is not null) BaselineContractPriceMinor = contractPriceMinor;

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

    /// <summary>
    /// Freezes the budget as the baseline: the sums of the active budget lines and the contract price are recorded, and from now on the
    /// budget changes only through approved change orders.
    /// </summary>
    public void LockBudgetBaseline(int activeLineCount, long activeNetMinor, long activeGrossMinor, Guid userId, DateTimeOffset now)
    {
        EnsureNotArchived();
        if (IsBudgetLocked) throw new DomainRuleException("The budget is already locked.");
        if (activeLineCount <= 0) throw new DomainRuleException("Add at least one budget line before locking the budget.");
        BudgetBaselineLockedAtUtc = now;
        BudgetBaselineLockedByUserId = userId;
        BaselineBudgetNetMinor = activeNetMinor;
        BaselineBudgetGrossMinor = activeGrossMinor;
        BaselineContractPriceMinor = ContractPriceMinor;
        Touch(now);
    }

    /// <summary>
    /// Opens the budget for direct editing again. Only while no change order is approved: once one is, the baseline is what explains the
    /// revised budget, and unlocking would let lines change with no record of why.
    /// </summary>
    public void UnlockBudgetBaseline(string? reason, bool anyApprovedChangeOrder, DateTimeOffset now)
    {
        EnsureNotArchived();
        if (!IsBudgetLocked) throw new DomainRuleException("The budget isn't locked.");
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for unlocking the budget", 300);
        if (anyApprovedChangeOrder)
            throw new DomainRuleException("The budget can't be unlocked because a change order has been approved. Record a change order to change it.");
        BudgetBaselineLockedAtUtc = null;
        BudgetBaselineLockedByUserId = null;
        BaselineBudgetNetMinor = null;
        BaselineBudgetGrossMinor = null;
        BaselineContractPriceMinor = null;
        BudgetBaselineUnlockReason = cleanReason;
        BudgetBaselineUnlockedAtUtc = now;
        Touch(now);
    }

    /// <summary>Guards direct changes to budget lines: a locked budget changes only through approved change orders.</summary>
    public void EnsureBudgetIsOpen()
    {
        EnsureNotArchived();
        if (IsBudgetLocked) throw new DomainRuleException(BudgetLockedMessage);
    }

    /// <summary>The contract price after adding <paramref name="deltaMinor"/>, or a field error when it would leave the allowed range.</summary>
    public long ContractPriceAfter(long deltaMinor, string field)
    {
        if (deltaMinor == 0) return ContractPriceMinor ?? 0;
        if (ContractPriceMinor is not { } price)
            throw new ValidationFailedException(field, "This project has no contract price yet. Enter the contract price on the project first.");
        if (deltaMinor is > MoneyLimits.MaximumAmountMinor or < -MoneyLimits.MaximumAmountMinor)
            throw new ValidationFailedException(field, "The change to the contract price can be at most ₱100,000,000,000.00 either way.");
        var result = price + deltaMinor;
        if (result < 0) throw new ValidationFailedException(field, "The contract price can't go below zero. Check the change to the price.");
        if (result > MoneyLimits.MaximumAmountMinor)
            throw new ValidationFailedException(field, "The contract price can be at most ₱100,000,000,000.00. Check the change to the price.");
        return result;
    }

    /// <summary>Moves the contract price by an approved (or voided) change order. Always marks the project changed, so two decisions on the
    /// same project at the same moment can't both be saved: the second is asked to reload.</summary>
    internal void ApplyChangeOrderDecision(long contractPriceDeltaMinor, DateTimeOffset now)
    {
        if (contractPriceDeltaMinor != 0) ContractPriceMinor = ContractPriceAfter(contractPriceDeltaMinor, "contractPriceDeltaMinor");
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
