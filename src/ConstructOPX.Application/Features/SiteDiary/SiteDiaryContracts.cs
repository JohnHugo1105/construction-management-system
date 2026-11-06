using System.Text.Json.Serialization;
using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.SiteDiary;

namespace ConstructOPX.Application.Features.SiteDiary;

// The site diary: one structured report per project per day. A report carries no money anywhere, and what is filled in beside it (the workers
// with attendance that day, the deliveries received that day) is read from what is already recorded and selects no pay figure and no price:
// see docs/business-rules.md section 18 and docs/api/site-diary.md. A test walks the property names of every response.

// ---- Ports ------------------------------------------------------------------------------------------------------------------

public interface ISiteDiaryRepository
{
    /// <summary>The project's live (not voided) report for the date, tracked for change. Null when there is none.</summary>
    Task<SiteDiaryEntry?> GetLiveAsync(Guid projectId, DateOnly reportDate, CancellationToken cancellationToken);

    Task<bool> LiveExistsAsync(Guid projectId, DateOnly reportDate, CancellationToken cancellationToken);

    /// <summary>The report (and its project and date) an earlier create with this request id made.</summary>
    Task<DiaryRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(SiteDiaryEntry entry);
}

/// <summary>Who a request id already belongs to.</summary>
public sealed record DiaryRequestOwner(Guid EntryId, Guid ProjectId, DateOnly ReportDate);

/// <summary>
/// Reads reports straight into DTOs. A project's list is two statements (a count and a page); one report is three or four (the report, the
/// workers' count, their names only for a caller allowed to see them, and the deliveries), however many reports, workers or deliveries exist.
/// </summary>
public interface ISiteDiaryReader
{
    Task<PagedResult<DiaryListItemDto>> ListAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The project's live (not voided) report for the date. Null when there is none.</summary>
    Task<DiaryEntryDto?> GetAsync(Guid projectId, DateOnly reportDate, DiaryReadScope scope, CancellationToken cancellationToken);

    /// <summary>One report by its id, whatever its state (a voided report is still readable). Null when it isn't in the organization.</summary>
    Task<DiaryEntryDto?> GetByIdAsync(Guid id, DiaryReadScope scope, CancellationToken cancellationToken);

    /// <summary>The workers with attendance and the deliveries received on the date, for a day that may not have a report yet.</summary>
    Task<DiaryFilledInDto> FilledInAsync(Guid projectId, DateOnly reportDate, bool includeNames, CancellationToken cancellationToken);
}

/// <summary>Who is reading a report, so the reader can say whether they may change it and whether they may see the workers' names.</summary>
/// <param name="CanManage">Holds <c>projects.manage</c> (Project Manager, Administrator): may change any report.</param>
/// <param name="CanWrite">Holds <c>diary.write</c>.</param>
/// <param name="CanSeeWorkerNames">Holds <c>workforce.read</c>.</param>
/// <param name="Today">The company's calendar day.</param>
public sealed record DiaryReadScope(Guid UserId, bool CanManage, bool CanWrite, bool CanSeeWorkerNames, DateOnly Today);

// ---- DTOs -------------------------------------------------------------------------------------------------------------------

/// <param name="WorkersOnSite">Workers with attendance recorded that day on the project (a count, never pay).</param>
/// <param name="SubcontractorWorkers">As typed in the report.</param>
/// <param name="WorkDoneFirstLine">The first line of "work done", cut short.</param>
public sealed record DiaryListItemDto(Guid Id, DateOnly ReportDate, string Weather, int WorkersOnSite, int SubcontractorWorkers, string WorkDoneFirstLine,
    string? AuthorName, int Version);

/// <summary>What is read beside a report instead of being typed: who was on site and what was delivered that day.</summary>
public sealed record DiaryFilledInDto(DiaryOnSiteDto OnSite, IReadOnlyList<DiaryDeliveryDto> Deliveries);

/// <param name="Count">Workers with attendance recorded that day on the project. Every reader gets the count.</param>
/// <param name="Names">The workers' names, only for a caller who may see the worker list (<c>workforce.read</c>); left out of the JSON for anyone else.</param>
public sealed record DiaryOnSiteDto(int Count,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Names);

/// <summary>One item received that day: the supplier, the item as ordered, its unit and the quantity. Never a price or a total.</summary>
public sealed record DiaryDeliveryDto(string SupplierName, string Description, string Unit, decimal Quantity);

/// <param name="Status">Posted or Void.</param>
/// <param name="CanChange">The caller may correct or void this report now (the author until the end of the next day, a Project Manager or an Administrator any time, on a project that is not archived).</param>
/// <param name="ChangeableUntil">The last day the author may change it; a Project Manager or an Administrator are not limited by it.</param>
public sealed record DiaryEntryDto(Guid Id, Guid ProjectId, string ProjectName, bool ProjectArchived, DateOnly ReportDate, string Weather, string? WeatherNote,
    string WorkDone, string? IssuesAndDelays, string? VisitorsAndInspections, string? SafetyNotes, int SubcontractorWorkers, string Status, string? AuthorName,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateOnly ChangeableUntil, bool CanChange, DiaryOnSiteDto OnSite,
    IReadOnlyList<DiaryDeliveryDto> Deliveries, int Version);

// ---- Requests ---------------------------------------------------------------------------------------------------------------

/// <param name="Weather">Fair, Cloudy, Rain, HeavyRain or Storm.</param>
public sealed record CreateDiaryEntryRequest(DateOnly? ReportDate, string? Weather, string? WeatherNote, string? WorkDone, string? IssuesAndDelays,
    string? VisitorsAndInspections, string? SafetyNotes, int SubcontractorWorkers, string? ClientRequestId);

public sealed record UpdateDiaryEntryRequest(string? Weather, string? WeatherNote, string? WorkDone, string? IssuesAndDelays, string? VisitorsAndInspections,
    string? SafetyNotes, int SubcontractorWorkers, int Version);

public sealed record VoidDiaryEntryRequest(string? Reason, int Version);

/// <summary>The first line of a longer text, cut to a length that fits a list row.</summary>
public static class DiaryPreview
{
    public const int MaximumLength = 140;

    public static string FirstLine(string? text)
    {
        var line = (text ?? "").Split('\n', 2)[0].Trim();
        return line.Length <= MaximumLength ? line : line[..(MaximumLength - 1)].TrimEnd() + "…";
    }
}
