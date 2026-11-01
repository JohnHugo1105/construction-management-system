using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Features.Subcontracts;
using ConstructOPX.Application.Features.Suppliers;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Request builders and steps shared by the subcontract API tests.</summary>
internal static class SubcontractTestKit
{
    /// <summary>A claim date safely in the past in every time zone; the period it covers ends the day before.</summary>
    public const string ClaimDate = "2025-12-01";
    public const string PeriodEnd = "2025-11-30";
    public const string PaidOn = "2025-12-05";

    public static async Task<Guid> SubcontractorAsync(ApiClient client, string? name = null, bool subcontractor = true) =>
        (await client.PostAsync<SupplierDto>("/api/suppliers", new
        {
            name = name ?? TenantSeeder.Unique("Masonry Crew"), isSupplier = !subcontractor, isSubcontractor = subcontractor, tin = (string?)null,
            contactPerson = (string?)null, phone = (string?)null, email = (string?)null, address = (string?)null, notes = (string?)null, version = 0
        })).Id;

    /// <summary>Defaults: 1,000,000.00 before VAT, 10% retention, 2% withholding.</summary>
    public static Dictionary<string, object?> NewSubcontract(Guid projectId, Guid firmId, long sumMinor = 1_000_000_00, string? vat = "Exclusive",
        int? retentionBps = 1_000, int? withholdingBps = 200, string? title = "Electrical works", string? clientRequestId = null,
        Guid? subProjectId = null) => new()
    {
        ["subcontractorId"] = firmId, ["projectId"] = projectId, ["subProjectId"] = subProjectId, ["title"] = title, ["notes"] = null,
        ["contractSumMinor"] = sumMinor, ["vatTreatment"] = vat, ["retentionBps"] = retentionBps, ["withholdingBps"] = withholdingBps,
        ["clientRequestId"] = clientRequestId
    };

    public static async Task<SubcontractDto> CreateAsync(ApiClient client, Guid projectId, Guid firmId, long sumMinor = 1_000_000_00,
        string vat = "Exclusive", int retentionBps = 1_000, int withholdingBps = 200) =>
        await client.PostAsync<SubcontractDto>("/api/subcontracts", NewSubcontract(projectId, firmId, sumMinor, vat, retentionBps, withholdingBps));

    public static async Task<SubcontractDto> AwardAsync(ApiClient client, SubcontractDto subcontract) =>
        await client.PostAsync<SubcontractDto>($"/api/subcontracts/{subcontract.Id}/award", new { version = subcontract.Version });

    /// <summary>A new project, firm and awarded subcontract (1,000,000.00 before VAT unless told otherwise).</summary>
    public static async Task<SubcontractDto> AwardedAsync(ApiClient client, Guid? projectId = null, long sumMinor = 1_000_000_00, string vat = "Exclusive")
    {
        var project = projectId ?? (await CostingTestKit.CreateProjectAsync(client)).Id;
        var firm = await SubcontractorAsync(client);
        return await AwardAsync(client, await CreateAsync(client, project, firm, sumMinor, vat));
    }

    public static object Claim(long cumulativeMinor, int version, string? clientRequestId = null, string claimDate = ClaimDate,
        string periodEnd = PeriodEnd) => new { claimDate, periodEnd, cumulativeWorkMinor = cumulativeMinor, notes = (string?)null, clientRequestId, version };

    public static Task<HttpResponseMessage> DraftClaimAsync(ApiClient client, SubcontractDto subcontract, long cumulativeMinor, string? clientRequestId = null) =>
        client.PostAsync($"/api/subcontracts/{subcontract.Id}/claims", Claim(cumulativeMinor, subcontract.Version, clientRequestId));

    public static async Task<SubcontractDto> DraftAsync(ApiClient client, SubcontractDto subcontract, long cumulativeMinor) =>
        await client.ReadAsync<SubcontractDto>(await DraftClaimAsync(client, subcontract, cumulativeMinor));

    public static SubcontractClaimDto OpenDraft(SubcontractDto subcontract) => subcontract.Claims.Single(claim => claim.Status == "Draft");

    public static Task<HttpResponseMessage> CertifyAsync(ApiClient client, SubcontractDto subcontract, Guid claimId) =>
        client.PostAsync($"/api/subcontracts/{subcontract.Id}/claims/{claimId}/certify", new { version = subcontract.Version });

    /// <summary>Drafts and certifies a claim for this much work to date.</summary>
    public static async Task<SubcontractDto> CertifiedAsync(ApiClient client, SubcontractDto subcontract, long cumulativeMinor)
    {
        var drafted = await DraftAsync(client, subcontract, cumulativeMinor);
        return await client.ReadAsync<SubcontractDto>(await CertifyAsync(client, drafted, OpenDraft(drafted).Id));
    }

    public static SubcontractClaimDto ClaimNumbered(SubcontractDto subcontract, int number) =>
        subcontract.Claims.Single(claim => claim.ClaimNumber == number && claim.Status == "Certified");

    public static object Payment(long amountMinor, int version, string? clientRequestId = null, string paidOn = PaidOn, string? method = "BankTransfer") =>
        new { amountMinor, paidOn, method, reference = "BDO-1001", note = (string?)null, clientRequestId, version };

    public static Task<HttpResponseMessage> PayAsync(ApiClient client, SubcontractDto subcontract, Guid claimId, long amountMinor, string? clientRequestId = null) =>
        client.PostAsync($"/api/subcontracts/{subcontract.Id}/claims/{claimId}/payments", Payment(amountMinor, subcontract.Version, clientRequestId));

    public static Task<HttpResponseMessage> VoidClaimAsync(ApiClient client, SubcontractDto subcontract, Guid claimId, string reason = "Measured wrongly") =>
        client.PostAsync($"/api/subcontracts/{subcontract.Id}/claims/{claimId}/void", new { reason, version = subcontract.Version });
}
