using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Receiving;
using ConstructOPX.Application.Features.Statutory;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Statutory;

namespace ConstructOPX.Application.Features.NeedsAction;

/// <summary>
/// The "Needs your action" queue of the signed-in person. The groups come from the caller's own roles (a group they cannot act on is not
/// read and not returned); each group is a count and its eight most urgent rows, read from statuses that already exist. Nothing is stored,
/// sent or scheduled. The full read costs the organization (for today's date) plus at most two SQL statements per group; the count costs
/// the organization plus one statement per group. A caller with no group costs nothing.
/// </summary>
public sealed class NeedsActionService(INeedsActionReader reader, IReceivingReader receiving, StatutoryService statutory,
    IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock)
{
    private static readonly ReceivingOrderFilter EveryOrder = new(null, null);

    public async Task<NeedsActionDto> GetAsync(CancellationToken cancellationToken)
    {
        var groups = NeedsActionGroups.For(currentUser.Roles);
        if (groups.Count == 0) return new NeedsActionDto([]);

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var dates = new NeedsActionDates(organization.TodayLocal(clock.UtcNow));
        // One context answers every group, so the groups are read one after another.
        var read = new List<NeedsActionGroupDto>(groups.Count);
        foreach (var group in groups) read.Add(await ReadAsync(group, dates, organization, cancellationToken));
        return new NeedsActionDto(NeedsActionGroups.Ordered(read));
    }

    public async Task<NeedsActionCountDto> CountAsync(CancellationToken cancellationToken)
    {
        var groups = NeedsActionGroups.For(currentUser.Roles);
        if (groups.Count == 0) return new NeedsActionCountDto(0);

        var dates = new NeedsActionDates((await organizations.GetCurrentAsync(cancellationToken)).TodayLocal(clock.UtcNow));
        var total = 0;
        foreach (var group in groups) total += await CountAsync(group, dates, cancellationToken);
        return new NeedsActionCountDto(total);
    }

    private async Task<int> CountAsync(NeedsActionGroup group, NeedsActionDates dates, CancellationToken cancellationToken) => group switch
    {
        NeedsActionGroup.DeliveriesExpected => await receiving.CountAsync(EveryOrder, cancellationToken),
        NeedsActionGroup.StatutoryNeedsSetup => (await TablesNeedingSetupAsync(dates, cancellationToken)).Count > 0 ? 1 : 0,
        _ => await reader.CountAsync(group, dates, cancellationToken)
    };

    private async Task<NeedsActionGroupDto> ReadAsync(NeedsActionGroup group, NeedsActionDates dates, Organization organization,
        CancellationToken cancellationToken)
    {
        var key = NeedsActionGroups.Key(group);
        switch (group)
        {
            case NeedsActionGroup.DeliveriesExpected:
            {
                // The receiving read model selects quantities and names only, so this group can never carry an amount.
                var page = await receiving.ListAsync(EveryOrder, new PageRequest(1, NeedsActionRules.RowsShown), cancellationToken);
                return new NeedsActionGroupDto(key, page.OrderCount, page.Items.Select(order => NeedsActionRows.Delivery(order, dates.Today)).ToArray());
            }
            case NeedsActionGroup.StatutoryNeedsSetup:
            {
                var missing = await TablesNeedingSetupAsync(dates, cancellationToken);
                return missing.Count == 0
                    ? new NeedsActionGroupDto(key, 0, [])
                    : new NeedsActionGroupDto(key, 1, [NeedsActionRows.Statutory(missing)]);
            }
            default:
            {
                var count = await reader.CountAsync(group, dates, cancellationToken);
                if (count == 0) return new NeedsActionGroupDto(key, 0, []);
                var records = await reader.TopAsync(group, dates, NeedsActionRules.RowsShown, cancellationToken);
                return new NeedsActionGroupDto(key, count,
                    records.Select(record => NeedsActionRows.From(group, record, dates.Today, organization.TodayLocal)).ToArray());
            }
        }
    }

    /// <summary>The agencies the statutory status reports as needing setup today, labelled for a sentence ("withholding tax" in lower case).</summary>
    private async Task<IReadOnlyList<string>> TablesNeedingSetupAsync(NeedsActionDates dates, CancellationToken cancellationToken)
    {
        var status = await statutory.StatusAsync(dates.Today, cancellationToken);
        return status.Agencies.Where(agency => agency.State == "NeedsSetup")
            .Select(agency => agency.Agency == nameof(StatutoryAgency.WithholdingTax) ? "withholding tax" : agency.Label).ToArray();
    }
}
