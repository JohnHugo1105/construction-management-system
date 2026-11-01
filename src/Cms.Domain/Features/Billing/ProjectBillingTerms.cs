using Cms.Domain.Common;
using Cms.Domain.Features.Projects;

namespace Cms.Domain.Features.Billing;

/// <summary>The terms a bill is made with, copied onto the bill so changing the project's terms later never alters a bill.</summary>
public sealed record BillingTermsSnapshot(int RetentionBps, int WithholdingBps, int PaymentTermsDays)
{
    /// <summary>What applies to a project whose terms were never saved: 10% retention, no withholding, payment due in 30 days.</summary>
    public static BillingTermsSnapshot Defaults { get; } =
        new(ProjectBillingTerms.DefaultRetentionBps, ProjectBillingTerms.DefaultWithholdingBps, ProjectBillingTerms.DefaultPaymentTermsDays);
}

/// <summary>
/// How a project is billed: the share of each progress bill the client retains until the project is completed, the share of the
/// amount before VAT the client withholds as creditable tax, and how many days the client has to pay. One per project, created the
/// first time someone saves them; until then the defaults apply.
/// </summary>
public sealed class ProjectBillingTerms : AggregateRoot
{
    public const int DefaultRetentionBps = 1_000;
    public const int MaximumRetentionBps = 3_000;
    public const int DefaultWithholdingBps = 0;
    public const int MaximumWithholdingBps = 1_000;
    public const int DefaultPaymentTermsDays = 30;
    public const int MaximumPaymentTermsDays = 180;

    private ProjectBillingTerms() { }

    public Guid ProjectId { get; private set; }
    public int RetentionBps { get; private set; }
    public int CreditableWithholdingBps { get; private set; }
    public int PaymentTermsDays { get; private set; }

    public BillingTermsSnapshot Snapshot => new(RetentionBps, CreditableWithholdingBps, PaymentTermsDays);

    public static ProjectBillingTerms Create(Guid id, Guid tenantId, Project project, int retentionBps, int withholdingBps, int paymentTermsDays,
        DateTimeOffset now)
    {
        project.EnsureNotArchived();
        Validate(retentionBps, withholdingBps, paymentTermsDays);
        var terms = new ProjectBillingTerms
        {
            ProjectId = project.Id,
            RetentionBps = retentionBps,
            CreditableWithholdingBps = withholdingBps,
            PaymentTermsDays = paymentTermsDays
        };
        terms.Initialize(id, tenantId, now);
        return terms;
    }

    /// <summary>Changes the terms for bills made from now on. Bills already made keep the terms they were made with.</summary>
    public void Change(Project project, int retentionBps, int withholdingBps, int paymentTermsDays, DateTimeOffset now)
    {
        if (project.Id != ProjectId) throw new ArgumentException("These terms belong to another project.", nameof(project));
        project.EnsureNotArchived();
        Validate(retentionBps, withholdingBps, paymentTermsDays);
        RetentionBps = retentionBps;
        CreditableWithholdingBps = withholdingBps;
        PaymentTermsDays = paymentTermsDays;
        Touch(now);
    }

    private static void Validate(int retentionBps, int withholdingBps, int paymentTermsDays)
    {
        var errors = new ValidationErrors();
        if (retentionBps is < 0 or > MaximumRetentionBps) errors.Add("retentionBps", "Enter a retention between 0% and 30%.");
        if (withholdingBps is < 0 or > MaximumWithholdingBps) errors.Add("creditableWithholdingBps", "Enter a withholding between 0% and 10%.");
        if (paymentTermsDays is < 0 or > MaximumPaymentTermsDays) errors.Add("paymentTermsDays", "Enter payment terms between 0 and 180 days.");
        errors.ThrowIfAny();
    }
}
