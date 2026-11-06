namespace ConstructOPX.Domain.Features.Costing;

/// <summary>How the unit cost on a line relates to VAT.</summary>
public enum VatTreatment
{
    /// <summary>The unit cost already includes VAT (what suppliers' receipts usually show). Net and VAT are split out of it.</summary>
    Inclusive = 0,

    /// <summary>The unit cost is before VAT. VAT is added on top.</summary>
    Exclusive = 1,

    /// <summary>No VAT applies to the line.</summary>
    Exempt = 2
}
