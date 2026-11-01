using Cms.Domain.Common;
using Cms.Domain.Features.Projects;

namespace Cms.Domain.Features.Billing;

/// <summary>
/// The terms a bill is made with, copied onto the bill so changing the project's terms later never alters a bill.
/// <paramref name="AdvanceBps"/> is the down payment as a share of the contract price; <paramref name="VatWithholdingBps"/> the share of
/// the amount before VAT the client withholds as VAT.
/// </summary>
public sealed record BillingTermsSnapshot(int RetentionBps, int WithholdingBps, int PaymentTermsDays, int AdvanceBps = 0, int VatWithholdingBps = 0)
{
    /// <summary>
    /// What applies to a project whose terms were never saved: 10% retention, no withholding, payment due in 30 days, no down payment,
    /// no VAT withholding.
    /// </summary>
    public static BillingTermsSnapshot Defaults { get; } =
        new(ProjectBillingTerms.DefaultRetentionBps, ProjectBillingTerms.DefaultWithholdingBps, ProjectBillingTerms.DefaultPaymentTermsDays,
            ProjectBillingTerms.DefaultAdvanceBps, ProjectBillingTerms.DefaultVatWithholdingBps);
}

/// <summary>
/// How a project is billed: the share of each progress bill the client retains until the project is completed, the share of the
/// amount before VAT the client withholds as creditable income tax and as VAT, how many days the client has to pay, the down payment
/// billed before the first progress bill, and whether the client is a government agency. One per project, created the first time
/// someone saves them; until then the defaults apply.
/// </summary>
public sealed class ProjectBillingTerms : AggregateRoot
{
    public const int DefaultRetentionBps = 1_000;
    public const int MaximumRetentionBps = 3_000;
    public const int DefaultWithholdingBps = 0;
    public const int MaximumWithholdingBps = 1_000;
    public const int DefaultPaymentTermsDays = 30;
    public const int MaximumPaymentTermsDays = 180;
    public const int DefaultAdvanceBps = 0;
    public const int MaximumAdvanceBps = 5_000;
    public const int DefaultVatWithholdingBps = 0;
    public const int MaximumVatWithholdingBps = 1_200;

    public const string GovernmentAdvanceMessage = "A government project allows a down payment of up to 15% of the contract price.";

    private ProjectBillingTerms() { }

    public Guid ProjectId { get; private set; }
    public int RetentionBps { get; private set; }
    public int CreditableWithholdingBps { get; private set; }
    public int PaymentTermsDays { get; private set; }

    /// <summary>The down payment as a share of the contract price; 0 when the project takes none.</summary>
    public int AdvanceBps { get; private set; }

    /// <summary>The share of the amount before VAT the client withholds as VAT and covers with its own certificate.</summary>
    public int VatWithholdingBps { get; private set; }

    /// <summary>The client is a government agency: the down payment is capped and added variation orders are watched.</summary>
    public bool IsGovernmentClient { get; private set; }

    public BillingTermsSnapshot Snapshot => new(RetentionBps, CreditableWithholdingBps, PaymentTermsDays, AdvanceBps, VatWithholdingBps);

    public static ProjectBillingTerms Create(Guid id, Guid tenantId, Project project, int retentionBps, int withholdingBps, int paymentTermsDays,
        DateTimeOffset now, int advanceBps = DefaultAdvanceBps, int vatWithholdingBps = DefaultVatWithholdingBps, bool isGovernmentClient = false)
    {
        project.EnsureNotArchived();
        Validate(retentionBps, withholdingBps, paymentTermsDays, advanceBps, vatWithholdingBps, isGovernmentClient);
        var terms = new ProjectBillingTerms
        {
            ProjectId = project.Id,
            RetentionBps = retentionBps,
            CreditableWithholdingBps = withholdingBps,
            PaymentTermsDays = paymentTermsDays,
            AdvanceBps = advanceBps,
            VatWithholdingBps = vatWithholdingBps,
            IsGovernmentClient = isGovernmentClient
        };
        terms.Initialize(id, tenantId, now);
        return terms;
    }

    /// <summary>
    /// Changes the terms for bills made from now on. Bills already made keep the terms they were made with, and a down payment already
    /// billed is recovered at the rate it was billed at.
    /// </summary>
    public void Change(Project project, int retentionBps, int withholdingBps, int paymentTermsDays, DateTimeOffset now, int? advanceBps = null,
        int? vatWithholdingBps = null, bool? isGovernmentClient = null)
    {
        if (project.Id != ProjectId) throw new ArgumentException("These terms belong to another project.", nameof(project));
        project.EnsureNotArchived();
        var advance = advanceBps ?? AdvanceBps;
        var vatWithholding = vatWithholdingBps ?? VatWithholdingBps;
        var government = isGovernmentClient ?? IsGovernmentClient;
        Validate(retentionBps, withholdingBps, paymentTermsDays, advance, vatWithholding, government);
        RetentionBps = retentionBps;
        CreditableWithholdingBps = withholdingBps;
        PaymentTermsDays = paymentTermsDays;
        AdvanceBps = advance;
        VatWithholdingBps = vatWithholding;
        IsGovernmentClient = government;
        Touch(now);
    }

    private static void Validate(int retentionBps, int withholdingBps, int paymentTermsDays, int advanceBps, int vatWithholdingBps, bool isGovernmentClient)
    {
        var errors = new ValidationErrors();
        if (retentionBps is < 0 or > MaximumRetentionBps) errors.Add("retentionBps", "Enter a retention between 0% and 30%.");
        if (withholdingBps is < 0 or > MaximumWithholdingBps) errors.Add("creditableWithholdingBps", "Enter a withholding between 0% and 10%.");
        if (paymentTermsDays is < 0 or > MaximumPaymentTermsDays) errors.Add("paymentTermsDays", "Enter payment terms between 0 and 180 days.");
        if (advanceBps is < 0 or > MaximumAdvanceBps) errors.Add("advanceBps", "Enter a down payment between 0% and 50% of the contract price.");
        else if (isGovernmentClient && advanceBps > GovernmentContract.MaximumAdvanceBps) errors.Add("advanceBps", GovernmentAdvanceMessage);
        if (vatWithholdingBps is < 0 or > MaximumVatWithholdingBps) errors.Add("vatWithholdingBps", "Enter a VAT withholding between 0% and 12%.");
        errors.ThrowIfAny();
    }
}
