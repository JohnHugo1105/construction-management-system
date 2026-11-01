using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Domain.Common;
using Cms.Domain.Features.Projects;

namespace Cms.Application.Features.ChangeOrders;

/// <summary>
/// The budget baseline: locking freezes the agreed budget and contract price, after which only approved change orders change them, so the
/// original, the approved changes and the revised figures always explain each other.
/// </summary>
public sealed class BudgetBaselineService(IChangeOrderReader reader, IChangeOrderRepository orders, IProjectRepository projects,
    IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public async Task<BudgetBaselineDto> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        Build(await reader.BaselineFiguresAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project"));

    /// <summary>Freezes the budget: records the sums of the active budget lines and the contract price as the original.</summary>
    public async Task<BudgetBaselineDto> LockAsync(Guid projectId, VersionRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadProject(projectId, cancellationToken);
        ConcurrencyGuard.Ensure(project.Version, request.Version);
        var figures = await reader.BaselineFiguresAsync(projectId, cancellationToken) ?? throw new NotFoundException("that project");

        project.LockBudgetBaseline(figures.ActiveLineCount, figures.ActiveNetMinor, figures.ActiveGrossMinor, currentUser.UserId, clock.UtcNow);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        audit.Record("BudgetBaseline.Locked", nameof(Project), project.Id,
            $"Locked the budget of {project.Name} at {CostingInput.MoneyText(figures.ActiveGrossMinor, organization.CurrencyCode)} with VAT " +
            $"({figures.ActiveLineCount} line{(figures.ActiveLineCount == 1 ? "" : "s")}).");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    /// <summary>Opens the budget for direct editing again; only while no change order is approved.</summary>
    public async Task<BudgetBaselineDto> UnlockAsync(Guid projectId, UnlockBudgetRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadProject(projectId, cancellationToken);
        ConcurrencyGuard.Ensure(project.Version, request.Version);
        var anyApproved = await orders.AnyApprovedAsync(projectId, cancellationToken);

        project.UnlockBudgetBaseline(request.Reason, anyApproved, clock.UtcNow);
        // The reason is typed text, so it stays on the project, not in the activity log.
        audit.Record("BudgetBaseline.Unlocked", nameof(Project), project.Id, $"Unlocked the budget of {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    /// <summary>Original + approved changes = revised. While locked, the active budget lines must add up to the revised budget exactly.</summary>
    internal static BudgetBaselineDto Build(BaselineFigures figures)
    {
        var locked = figures.LockedAtUtc is not null;
        if (!locked)
        {
            return new BudgetBaselineDto(figures.ProjectId, false, null, null, null, null, null, 0, 0, 0, 0, figures.ActiveNetMinor,
                figures.ActiveGrossMinor, figures.ContractPriceMinor, figures.ActiveLineCount, figures.ActiveNetMinor, figures.ActiveGrossMinor,
                figures.ContractPriceMinor, figures.PendingCount, figures.PendingNetMinor, figures.PendingGrossMinor, figures.PendingPriceDeltaMinor,
                Reconciles: true, figures.LastUnlockReason, figures.LastUnlockedAtUtc, figures.ProjectVersion);
        }

        var originalNet = figures.BaselineBudgetNetMinor ?? 0;
        var originalGross = figures.BaselineBudgetGrossMinor ?? 0;
        var revisedNet = checked(originalNet + figures.ApprovedNetMinor);
        var revisedGross = checked(originalGross + figures.ApprovedGrossMinor);
        long? revisedPrice = figures.BaselineContractPriceMinor is { } price
            ? checked(price + figures.ApprovedPriceDeltaMinor)
            : figures.ApprovedPriceDeltaMinor == 0 ? null : figures.ApprovedPriceDeltaMinor;
        var reconciles = revisedNet == figures.ActiveNetMinor && revisedGross == figures.ActiveGrossMinor && revisedPrice == figures.ContractPriceMinor;

        return new BudgetBaselineDto(figures.ProjectId, true, figures.LockedAtUtc, figures.LockedBy, originalNet, originalGross,
            figures.BaselineContractPriceMinor, figures.ApprovedCount, figures.ApprovedNetMinor, figures.ApprovedGrossMinor,
            figures.ApprovedPriceDeltaMinor, revisedNet, revisedGross, revisedPrice, figures.ActiveLineCount, figures.ActiveNetMinor,
            figures.ActiveGrossMinor, figures.ContractPriceMinor, figures.PendingCount, figures.PendingNetMinor, figures.PendingGrossMinor,
            figures.PendingPriceDeltaMinor, reconciles, figures.LastUnlockReason, figures.LastUnlockedAtUtc, figures.ProjectVersion);
    }

    private async Task<Project> LoadProject(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");
}
