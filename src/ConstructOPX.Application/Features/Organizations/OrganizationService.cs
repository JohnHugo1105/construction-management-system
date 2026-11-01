using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Organizations;

namespace ConstructOPX.Application.Features.Organizations;

public interface IOrganizationRepository
{
    /// <summary>The signed-in person's organization.</summary>
    Task<Organization> GetCurrentAsync(CancellationToken cancellationToken);

    /// <summary>Finds any organization by id, bypassing the tenant filter (used while building a session).</summary>
    Task<Organization?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(Organization organization);
}

public sealed record OrganizationDto(Guid Id, string Name, string CurrencyCode, int VatRateBps, string TimeZoneId, decimal HoursPerDay,
    int OvertimeMultiplierBps, string VarianceBasis, int Version, int PayrollRunsPerMonth = Organization.DefaultPayrollRunsPerMonth);

public sealed record UpdateOrganizationRequest(string? Name, string? CurrencyCode, int VatRateBps, string? TimeZoneId, decimal HoursPerDay,
    int OvertimeMultiplierBps, string? VarianceBasis, int Version, int? PayrollRunsPerMonth = null);

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
            request.OvertimeMultiplierBps, basis, clock.UtcNow, request.PayrollRunsPerMonth);
        audit.Record("Organization.SettingsChanged", nameof(Organization), organization.Id,
            vatBefore == organization.VatRateBps
                ? "Changed the company settings."
                : $"Changed the company settings, including the VAT rate from {vatBefore / 100m:0.##}% to {organization.VatRateBps / 100m:0.##}% (existing lines keep their own rate).");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(organization);
    }

    internal static OrganizationDto ToDto(Organization organization) => new(organization.Id, organization.Name, organization.CurrencyCode,
        organization.VatRateBps, organization.TimeZoneId, organization.HoursPerDay, organization.OvertimeMultiplierBps,
        organization.VarianceBasis.ToString(), organization.Version, organization.PayrollRunsPerMonth);
}
