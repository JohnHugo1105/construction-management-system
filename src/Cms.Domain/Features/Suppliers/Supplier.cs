using Cms.Domain.Common;

namespace Cms.Domain.Features.Suppliers;

/// <summary>What a party is to the company. One party can be both: a firm that supplies materials and also does subcontract work.</summary>
[Flags]
public enum SupplierKind
{
    None = 0,
    Supplier = 1,
    Subcontractor = 2
}

/// <summary>
/// A firm the company buys from or hires: the master record that purchase orders and subcontracts point at, so one firm is spelled one
/// way everywhere. Archived, never deleted, once anything refers to it (the history must keep saying who was paid).
/// </summary>
public sealed class Supplier : AggregateRoot
{
    private Supplier() { }

    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";
    public SupplierKind Kind { get; private set; }

    /// <summary>The firm's tax identification number, as printed on its invoices.</summary>
    public string? Tin { get; private set; }

    public string? ContactPerson { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? Notes { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public bool IsSubcontractor => Kind.HasFlag(SupplierKind.Subcontractor);
    public bool IsSupplier => Kind.HasFlag(SupplierKind.Supplier);

    public static Supplier Create(Guid id, Guid tenantId, string? name, SupplierKind kind, string? tin, string? contactPerson, string? phone,
        string? email, string? address, string? notes, DateTimeOffset now)
    {
        var supplier = new Supplier();
        supplier.Initialize(id, tenantId, now);
        supplier.Apply(name, kind, tin, contactPerson, phone, email, address, notes);
        return supplier;
    }

    public void Update(string? name, SupplierKind kind, string? tin, string? contactPerson, string? phone, string? email, string? address,
        string? notes, DateTimeOffset now)
    {
        Apply(name, kind, tin, contactPerson, phone, email, address, notes);
        Touch(now);
    }

    public void Archive(DateTimeOffset now)
    {
        if (IsArchived) throw new DomainRuleException("This firm is already archived.");
        IsArchived = true;
        ArchivedAtUtc = now;
        Touch(now);
    }

    public void Restore(DateTimeOffset now)
    {
        if (!IsArchived) throw new DomainRuleException("Only an archived firm can be restored.");
        IsArchived = false;
        ArchivedAtUtc = null;
        Touch(now);
    }

    /// <summary>Refuses a new order or contract with a firm that is archived or isn't the right kind of firm.</summary>
    public void EnsureCanBeUsedAs(SupplierKind needed)
    {
        if (IsArchived) throw new DomainRuleException($"{Name} is archived. Restore it before using it again.");
        if (!Kind.HasFlag(needed))
            throw new DomainRuleException(needed == SupplierKind.Subcontractor
                ? $"{Name} isn't set up as a subcontractor. Edit the firm and tick \"Subcontractor\" first."
                : $"{Name} isn't set up as a supplier. Edit the firm and tick \"Supplier\" first.");
    }

    private void Apply(string? name, SupplierKind kind, string? tin, string? contactPerson, string? phone, string? email, string? address,
        string? notes)
    {
        var errors = new ValidationErrors();
        string cleanName = "";
        try { cleanName = Guard.RequiredText(name, "name", "the firm's name", 120); }
        catch (ValidationFailedException ex) { errors.Add("name", ex.Errors["name"][0]); }
        if (kind == SupplierKind.None || (kind & ~(SupplierKind.Supplier | SupplierKind.Subcontractor)) != 0)
            errors.Add("kind", "Choose whether this firm is a supplier, a subcontractor, or both.");
        var cleanEmail = Capture(errors, () => Guard.OptionalText(email, "email", "the email address", 254));
        if (cleanEmail is not null && (!cleanEmail.Contains('@') || cleanEmail.Contains(' ')))
            errors.Add("email", "Enter an email address like name@company.com, or leave it empty.");
        var cleanTin = Capture(errors, () => Guard.OptionalText(tin, "tin", "the tax number", 30));
        var cleanContact = Capture(errors, () => Guard.OptionalText(contactPerson, "contactPerson", "the contact person", 120));
        var cleanPhone = Capture(errors, () => Guard.OptionalText(phone, "phone", "the phone number", 40));
        var cleanAddress = Capture(errors, () => Guard.OptionalText(address, "address", "the address", 250));
        var cleanNotes = Capture(errors, () => Guard.OptionalText(notes, "notes", "the notes", 500));
        errors.ThrowIfAny();

        Name = cleanName;
        NormalizedName = Guard.Normalize(cleanName);
        Kind = kind;
        Tin = cleanTin;
        ContactPerson = cleanContact;
        Phone = cleanPhone;
        Email = cleanEmail;
        Address = cleanAddress;
        Notes = cleanNotes;
    }

    private static string? Capture(ValidationErrors errors, Func<string?> read)
    {
        try { return read(); }
        catch (ValidationFailedException ex)
        {
            foreach (var (field, messages) in ex.Errors)
                foreach (var message in messages) errors.Add(field, message);
            return null;
        }
    }
}
