using Cms.Application.Common;
using Cms.Domain.Features.Payroll;

namespace Cms.Application.Features.Payroll;

public interface IPayrollRepository
{
    /// <summary>Loads a run with its lines, deductions and project shares, tracked for change. Null when it isn't in the organization.</summary>
    Task<PayrollRun?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The run a form submission already created, so a double click returns it instead of reserving attendance twice.</summary>
    Task<Guid?> FindIdByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(PayrollRun run);
}

public interface IPayrollReader
{
    Task<PagedResult<PayrollRunListItemDto>> ListAsync(PayrollRunListQuery query, PageRequest page, CancellationToken cancellationToken);

    Task<PayrollRunDto?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record PayrollRunListQuery(string? Status);

public sealed record PayrollRunListItemDto(Guid Id, DateOnly PeriodStart, DateOnly PeriodEnd, string Status, int WorkerCount, long TotalGrossMinor,
    long TotalDeductionsMinor, long TotalNetMinor, DateTimeOffset CreatedAtUtc, DateTimeOffset? ApprovedAtUtc, int Version);

public sealed record PayrollDeductionDto(Guid Id, string Description, long AmountMinor);

/// <summary>The part of one worker's pay that belongs to one project (and sub-project). The shares add up to the line's gross pay.</summary>
public sealed record PayrollAllocationDto(Guid ProjectId, string ProjectName, Guid? SubProjectId, string? SubProjectName, decimal RegularHours,
    decimal OvertimeHours, long GrossMinor);

public sealed record PayrollLineDto(Guid WorkerId, string WorkerName, string EmployeeNumber, string Designation, decimal RegularHours,
    decimal OvertimeHours, long RegularPayMinor, long OvertimePayMinor, long GrossPayMinor, long DeductionsMinor, long NetPayMinor,
    IReadOnlyList<PayrollDeductionDto> Deductions, IReadOnlyList<PayrollAllocationDto> Allocations);

public sealed record PayrollRunDto(Guid Id, DateOnly PeriodStart, DateOnly PeriodEnd, string Status, int WorkerCount, long TotalGrossMinor,
    long TotalDeductionsMinor, long TotalNetMinor, decimal HoursPerDay, int OvertimeMultiplierBps, string? CreatedByName, DateTimeOffset CreatedAtUtc,
    string? ApprovedByName, DateTimeOffset? ApprovedAtUtc, string? VoidedByName, DateTimeOffset? VoidedAtUtc, string? VoidReason,
    IReadOnlyList<PayrollLineDto> Lines, int Version);

public sealed record CreatePayrollRunRequest(DateOnly PeriodStart, DateOnly PeriodEnd, string? ClientRequestId);

public sealed record DeductionInput(string? Description, long AmountMinor);

public sealed record SetDeductionsRequest(IReadOnlyList<DeductionInput>? Deductions, int Version);

public sealed record ApprovePayrollRunRequest(int Version);

public sealed record VoidPayrollRunRequest(string? Reason, int Version);

/// <summary>A downloadable payroll register.</summary>
public sealed record PayrollExportFile(string FileName, string ContentType, byte[] Content);
