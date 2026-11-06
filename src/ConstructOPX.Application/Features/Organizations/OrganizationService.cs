using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.TimeClock;

namespace ConstructOPX.Application.Features.Organizations;

public interface IOrganizationRepository
{
    /// <summary>The signed-in person's organization.</summary>
    Task<Organization> GetCurrentAsync(CancellationToken cancellationToken);

    /// <summary>Finds any organization by id, bypassing the tenant filter (used while building a session).</summary>
    Task<Organization?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(Organization organization);
}

/// <param name="TimeClockDayStartMinutes">When a work day starts for the time clock, in minutes after midnight (240 is 04:00).</param>
/// <param name="UnpaidBreakMinutes">The unpaid break taken off a single time-clock span of 5 hours or more (0 is none).</param>
/// <param name="PunchRoundingMinutes">A time-clock day's worked time is rounded to the nearest this many minutes (0 keeps the exact minutes).</param>
public sealed record OrganizationDto(Guid Id, string Name, string CurrencyCode, int VatRateBps, string TimeZoneId, decimal HoursPerDay,
    int OvertimeMultiplierBps, string VarianceBasis, int Version, int PayrollRunsPerMonth = Organization.DefaultPayrollRunsPerMonth,
    int TimeClockDayStartMinutes = TimeClockRules.DefaultDayStartMinutes, int UnpaidBreakMinutes = TimeClockRules.DefaultUnpaidBreakMinutes,
    int PunchRoundingMinutes = TimeClockRules.DefaultRoundingMinutes);

/// <summary>A time clock setting left out (null) stays as it is.</summary>
public sealed record UpdateOrganizationRequest(string? Name, string? CurrencyCode, int VatRateBps, string? TimeZoneId, decimal HoursPerDay,
    int OvertimeMultiplierBps, string? VarianceBasis, int Version, int? PayrollRunsPerMonth = null, int? TimeClockDayStartMinutes = null,
    int? UnpaidBreakMinutes = null, int? PunchRoundingMinutes = null);

public sealed class OrganizationService(IOrganizationRepository organizations, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public async Task<OrganizationDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await organizations.GetCurrentAsync(cancellationToken));

    public async Task<OrganizationDto> UpdateAsync(UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        ConcurrencyGuard.Ensure(organization.Version, request.Version);
        if (!Enum.TryParse<VarianceBasis>(request.VarianceBasis, ignoreCase: true, out var basis) || !Enum.IsDefined(basis))
            throw new ValidationFailedException("varianceBasis", "Choose whether to compare amounts with or without VAT.");

        var vatBefore = organization.VatRateBps;
        organization.UpdateSettings(request.Name ?? "", request.CurrencyCode ?? "", request.VatRateBps, request.TimeZoneId ?? "", request.HoursPerDay,
            request.OvertimeMultiplierBps, basis, clock.UtcNow, request.PayrollRunsPerMonth, request.TimeClockDayStartMinutes, request.UnpaidBreakMinutes,
            request.PunchRoundingMinutes);
        audit.Record("Organization.SettingsChanged", nameof(Organization), organization.Id,
            vatBefore == organization.VatRateBps
                ? "Changed the company settings."
                : $"Changed the company settings, including the VAT rate from {vatBefore / 100m:0.##}% to {organization.VatRateBps / 100m:0.##}% (existing lines keep their own rate).");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(organization);
    }

    internal static OrganizationDto ToDto(Organization organization) => new(organization.Id, organization.Name, organization.CurrencyCode,
        organization.VatRateBps, organization.TimeZoneId, organization.HoursPerDay, organization.OvertimeMultiplierBps,
        organization.VarianceBasis.ToString(), organization.Version, organization.PayrollRunsPerMonth, organization.TimeClockDayStartMinutes,
        organization.UnpaidBreakMinutes, organization.PunchRoundingMinutes);
}
