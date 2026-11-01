using System.Globalization;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Projects;

namespace ConstructOPX.Domain.Features.Bonds;

/// <summary>What a person types about a bond. Everything except the project and the direction can be changed while the bond is Active.</summary>
public sealed record BondDetails(BondKind Kind, string? IssuerName, string? BondNumber, long AmountMinor, DateOnly IssuedOn, DateOnly ValidUntil,
    string? OtherPartyName, string? Note);

/// <summary>
/// A performance, advance-payment, bid or warranty bond tied to a project, with the date it is valid until. A bond is a guarantee a bank or
/// surety gives, not a cost: it changes no budget, cost, billing or payment figure anywhere, it is only tracked so that nobody lets one lapse
/// unnoticed. The company posts bonds to its clients and receives them from its subcontractors and suppliers.
/// </summary>
/// <remarks>
/// An Active bond is edited, extended to a later date, released (returned), marked as called (the beneficiary claimed it) or voided (entered by
/// mistake). Released and Called are final: nothing else changes them, and a wrong one is voided and entered again. An Active bond whose date
/// has passed stays Active and is shown as Expired until a person releases or voids it. Every step writes an entry to the bond's own history;
/// the project must not be archived for any of them.
/// </remarks>
public sealed class Bond : AggregateRoot
{
    /// <summary>An Active bond with this many days or fewer left is "expiring soon". A business default, not a setting.</summary>
    public const int ExpiringSoonDays = 30;

    public const int MaximumNameLength = 120;
    public const int MaximumNumberLength = 60;
    public const int MaximumNoteLength = 500;
    public const int MaximumReasonLength = 300;

    private static readonly DateOnly EarliestDate = new(2000, 1, 1);
    private static readonly DateOnly LatestDate = new(2100, 12, 31);

    private readonly List<BondEvent> _events = [];

    private Bond() { }

    public Guid ProjectId { get; private set; }

    /// <summary>Posted (the company gave it) or Received (a subcontractor or supplier gave it to the company). Fixed once recorded.</summary>
    public BondDirection Direction { get; private set; }

    public BondKind Kind { get; private set; }

    /// <summary>The bank or surety that issued it.</summary>
    public string IssuerName { get; private set; } = "";

    public string BondNumber { get; private set; } = "";

    /// <summary>The guaranteed amount in minor units. Shown for reference; it is never added to any cost or billing figure.</summary>
    public long AmountMinor { get; private set; }

    /// <summary>The company currency when the bond was recorded.</summary>
    public string CurrencyCode { get; private set; } = "";

    public DateOnly IssuedOn { get; private set; }

    /// <summary>The last day the bond is in force.</summary>
    public DateOnly ValidUntil { get; private set; }

    /// <summary>For a Received bond, the subcontractor or supplier who gave it. Always empty for a Posted bond.</summary>
    public string? OtherPartyName { get; private set; }

    public string? Note { get; private set; }
    public BondStatus Status { get; private set; } = BondStatus.Active;

    public DateOnly? ReleasedOn { get; private set; }
    public string? ReleaseNote { get; private set; }
    public DateOnly? CalledOn { get; private set; }
    public string? CallNote { get; private set; }
    public string? VoidReason { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public string? ClientRequestId { get; private set; }

    public IReadOnlyList<BondEvent> Events => _events;

    private BondDetails Current => new(Kind, IssuerName, BondNumber, AmountMinor, IssuedOn, ValidUntil, OtherPartyName, Note);

    // ---- Expiry -----------------------------------------------------------------------------------------------------------------

    /// <summary>Whole days from <paramref name="today"/> to the valid-until date: zero on the last day, negative once it has passed.</summary>
    public static int DaysLeft(DateOnly validUntil, DateOnly today) => validUntil.DayNumber - today.DayNumber;

    /// <summary>Valid, ExpiringSoon (<see cref="ExpiringSoonDays"/> days or fewer left, including the last day) or Expired (the date has passed).</summary>
    public static BondExpiry ExpiryFor(DateOnly validUntil, DateOnly today)
    {
        var daysLeft = DaysLeft(validUntil, today);
        return daysLeft < 0 ? BondExpiry.Expired : daysLeft <= ExpiringSoonDays ? BondExpiry.ExpiringSoon : BondExpiry.Valid;
    }

    /// <summary>The expiry state of an Active bond on the company's calendar day; null for a bond that is no longer Active.</summary>
    public BondExpiry? ExpiryOn(DateOnly today) => Status == BondStatus.Active ? ExpiryFor(ValidUntil, today) : null;

    // ---- Lifecycle --------------------------------------------------------------------------------------------------------------

    public static Bond Create(Guid id, Guid tenantId, Project project, BondDirection direction, BondDetails details, string currencyCode,
        Guid createdByUserId, string? clientRequestId, DateTimeOffset now)
    {
        project.EnsureNotArchived();
        var bond = new Bond { ProjectId = project.Id, CurrencyCode = currencyCode, CreatedByUserId = createdByUserId };
        var errors = new ValidationErrors();
        if (!Enum.IsDefined(direction)) errors.Add("direction", "Choose whether the bond was posted by you or received from a subcontractor or supplier.");
        bond.Direction = direction;
        bond.ClientRequestId = Field(errors, () => Guard.OptionalText(clientRequestId, "clientRequestId", "the request id", 80));
        bond.ApplyDetails(details, errors);
        errors.ThrowIfAny();

        bond.Initialize(id, tenantId, now);
        bond.AddEvent(BondEventKind.Recorded, createdByUserId, now);
        return bond;
    }

    /// <summary>
    /// Changes an Active bond's details. The valid-until date can stay or move later; moving it earlier is refused. Returns false, and records
    /// nothing, when the details are the same as they were.
    /// </summary>
    public bool Edit(Project project, BondDetails details, Guid userId, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsureActive("changed");

        var previousValidUntil = ValidUntil;
        var before = Current;
        var errors = new ValidationErrors();
        ApplyDetails(details, errors, previousValidUntil);
        errors.ThrowIfAny();
        if (Current == before) return false; // nothing changed: no history line, no new version

        // A later valid-until date is an extension, so it is recorded with the old and new date even when it came through an edit.
        var moved = ValidUntil != previousValidUntil;
        AddEvent(BondEventKind.Edited, userId, now, previousValidUntil: moved ? previousValidUntil : null, newValidUntil: moved ? ValidUntil : null);
        Touch(now);
        return true;
    }

    /// <summary>Moves the valid-until date later (a renewal or extension by the bank or surety). Every extension is recorded.</summary>
    public void Extend(Project project, DateOnly newValidUntil, string? note, Guid userId, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsureActive("extended");

        var errors = new ValidationErrors();
        if (newValidUntil == default) errors.Add("validUntil", "Choose the new valid-until date.");
        else if (newValidUntil < EarliestDate || newValidUntil > LatestDate) errors.Add("validUntil", "Choose a valid date.");
        else if (newValidUntil <= ValidUntil)
            errors.Add("validUntil", $"The new date must be later than the current valid-until date, {Format(ValidUntil)}.");
        var cleanNote = Field(errors, () => Guard.OptionalText(note, "note", "the note", MaximumNoteLength));
        errors.ThrowIfAny();

        var previous = ValidUntil;
        ValidUntil = newValidUntil;
        AddEvent(BondEventKind.Extended, userId, now, previousValidUntil: previous, newValidUntil: newValidUntil, note: cleanNote);
        Touch(now);
    }

    /// <summary>The bond was returned or cancelled: it is no longer needed. Final.</summary>
    public void Release(Project project, DateOnly releasedOn, string? note, Guid userId, DateOnly today, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsureActive("released");

        var errors = new ValidationErrors();
        CheckEventDate(errors, releasedOn, "releasedOn", "the date it was released", today);
        var cleanNote = Field(errors, () => Guard.OptionalText(note, "note", "the note", MaximumNoteLength));
        errors.ThrowIfAny();

        Status = BondStatus.Released;
        ReleasedOn = releasedOn;
        ReleaseNote = cleanNote;
        AddEvent(BondEventKind.Released, userId, now, eventDate: releasedOn, note: cleanNote);
        Touch(now);
    }

    /// <summary>The beneficiary claimed against the bond. A date and a note on what happened are required. Final.</summary>
    public void MarkCalled(Project project, DateOnly calledOn, string? note, Guid userId, DateOnly today, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        EnsureActive("marked as called");

        var errors = new ValidationErrors();
        CheckEventDate(errors, calledOn, "calledOn", "the date it was called", today);
        var cleanNote = Field(errors, () => Guard.RequiredText(note, "note", "a note on what was claimed", MaximumNoteLength));
        errors.ThrowIfAny();

        Status = BondStatus.Called;
        CalledOn = calledOn;
        CallNote = cleanNote;
        AddEvent(BondEventKind.Called, userId, now, eventDate: calledOn, note: cleanNote);
        Touch(now);
    }

    /// <summary>
    /// Marks a bond that was entered by mistake. It stays on record with the reason and counts in nothing. A released or called bond that was
    /// recorded wrongly is voided here and entered again, because those two states are otherwise final.
    /// </summary>
    public void Void(Project project, string? reason, Guid userId, DateTimeOffset now)
    {
        EnsureProject(project);
        project.EnsureNotArchived();
        if (Status == BondStatus.Void) throw new DomainRuleException("This bond was already voided.");
        var cleanReason = Guard.RequiredText(reason, "reason", "the reason for voiding it", MaximumReasonLength);

        Status = BondStatus.Void;
        VoidReason = cleanReason;
        AddEvent(BondEventKind.Voided, userId, now, note: cleanReason);
        Touch(now);
    }

    // ---- Checks -----------------------------------------------------------------------------------------------------------------

    private void ApplyDetails(BondDetails details, ValidationErrors errors, DateOnly? currentValidUntil = null)
    {
        if (!Enum.IsDefined(details.Kind)) errors.Add("kind", "Choose the kind of bond: bid, performance, advance payment, warranty or other.");
        var issuer = Field(errors, () => Guard.RequiredText(details.IssuerName, "issuerName", "the bank or surety that issued it", MaximumNameLength));
        var number = Field(errors, () => Guard.RequiredText(details.BondNumber, "bondNumber", "the bond number", MaximumNumberLength));
        var otherParty = Field(errors, () => Guard.OptionalText(details.OtherPartyName, "otherPartyName", "the firm's name", MaximumNameLength));
        var note = Field(errors, () => Guard.OptionalText(details.Note, "note", "the note", MaximumNoteLength));

        if (details.AmountMinor <= 0) errors.Add("amountMinor", "Enter the bond amount, more than zero.");
        else MoneyLimits.CheckAmount(errors, details.AmountMinor, "amountMinor", "the bond amount");

        if (otherParty is not null && Direction == BondDirection.Posted)
            errors.Add("otherPartyName", "Only a bond you received names the firm that gave it. Leave this empty for a bond you posted.");

        var issuedOk = CheckDate(errors, details.IssuedOn, "issuedOn", "the date the bond was issued");
        var validOk = CheckDate(errors, details.ValidUntil, "validUntil", "the date the bond is valid until");
        if (issuedOk && validOk && details.ValidUntil < details.IssuedOn)
            errors.Add("validUntil", "The bond can't be valid until a date before it was issued.");
        else if (validOk && currentValidUntil is { } current && details.ValidUntil < current)
            errors.Add("validUntil", $"An active bond can't be changed to an earlier date than {Format(current)}. Extend it to move the date later.");

        if (errors.Any) return;
        Kind = details.Kind;
        IssuerName = issuer!;
        BondNumber = number!;
        AmountMinor = details.AmountMinor;
        IssuedOn = details.IssuedOn;
        ValidUntil = details.ValidUntil;
        OtherPartyName = otherParty;
        Note = note;
    }

    private static bool CheckDate(ValidationErrors errors, DateOnly date, string field, string label)
    {
        if (date == default) errors.Add(field, $"Choose {label}.");
        else if (date < EarliestDate || date > LatestDate) errors.Add(field, "Choose a valid date.");
        else return true;
        return false;
    }

    private void CheckEventDate(ValidationErrors errors, DateOnly date, string field, string label, DateOnly today)
    {
        if (!CheckDate(errors, date, field, label)) return;
        if (date > today) errors.Add(field, "The date can't be in the future.");
        else if (date < IssuedOn) errors.Add(field, $"The date can't be before the bond was issued on {Format(IssuedOn)}.");
    }

    private void EnsureActive(string doing)
    {
        switch (Status)
        {
            case BondStatus.Active: return;
            case BondStatus.Released:
                throw new DomainRuleException($"This bond was released, so it can't be {doing}. If the release was a mistake, void this bond and enter it again.");
            case BondStatus.Called:
                throw new DomainRuleException($"This bond was called, so it can't be {doing}. If that was a mistake, void this bond and enter it again.");
            default:
                throw new DomainRuleException($"This bond was voided, so it can't be {doing}.");
        }
    }

    private void EnsureProject(Project project)
    {
        if (project.Id != ProjectId) throw new ArgumentException("The project doesn't match this bond.", nameof(project));
    }

    private void AddEvent(BondEventKind kind, Guid userId, DateTimeOffset now, DateOnly? previousValidUntil = null, DateOnly? newValidUntil = null,
        DateOnly? eventDate = null, string? note = null) =>
        _events.Add(new BondEvent(Guid.NewGuid(), TenantId, Id, kind, userId, now, previousValidUntil, newValidUntil, eventDate, note));

    /// <summary>"3 Dec 2026", for messages.</summary>
    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static T? Field<T>(ValidationErrors errors, Func<T> read)
    {
        try { return read(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
            return default;
        }
    }
}

/// <summary>One line of a bond's history: what happened, who did it and when. Append-only.</summary>
public sealed class BondEvent
{
    private BondEvent() { }

    internal BondEvent(Guid id, Guid tenantId, Guid bondId, BondEventKind kind, Guid byUserId, DateTimeOffset atUtc, DateOnly? previousValidUntil,
        DateOnly? newValidUntil, DateOnly? eventDate, string? note)
    {
        Id = id;
        TenantId = tenantId;
        BondId = bondId;
        Kind = kind;
        ByUserId = byUserId;
        AtUtc = atUtc;
        PreviousValidUntil = previousValidUntil;
        NewValidUntil = newValidUntil;
        EventDate = eventDate;
        Note = note;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BondId { get; private set; }
    public BondEventKind Kind { get; private set; }
    public Guid ByUserId { get; private set; }
    public DateTimeOffset AtUtc { get; private set; }

    /// <summary>For an extension, or an edit that moved the date: the valid-until date before and after.</summary>
    public DateOnly? PreviousValidUntil { get; private set; }

    public DateOnly? NewValidUntil { get; private set; }

    /// <summary>For a release or a call: the date it happened.</summary>
    public DateOnly? EventDate { get; private set; }

    /// <summary>The note or reason a person gave with the step. It stays on the record, never in the audit line.</summary>
    public string? Note { get; private set; }
}
