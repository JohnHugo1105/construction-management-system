using ConstructOPX.Api.Security;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.TimeClock;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// The time clock for people: reviewing and confirming a day of scans (timeclock.review), badge codes, badge cards and imports (timeclock.manage), and
/// the registered machines (settings.manage). Confirming a day creates attendance through the attendance rules; nothing here creates pay. The machine
/// route is <see cref="TimeClockMachineEndpoints"/>.
/// </summary>
public sealed class TimeClockEndpoints : IEndpointModule
{
    /// <summary>The largest import body accepted, in bytes (the file's text itself is limited to about a million characters).</summary>
    private const long MaximumImportBytes = 2_500_000;

    public void Map(RouteGroupBuilder api)
    {
        var clock = api.MapFeature("/timeclock", "Time clock");

        // ---- Review (timeclock.review) ----
        clock.MapGet("/days", async ([FromQuery] Guid projectId, [FromQuery] DateOnly? date, [AsParameters] PageQuery page, TimeClockReviewService service,
                CancellationToken ct) => Results.Ok(await service.DaysAsync(projectId, date, page.ToRequest(), ct)))
            .RequirePermission(Permissions.TimeClockReview);

        clock.MapGet("/summary", async ([FromQuery] Guid projectId, TimeClockReviewService service, CancellationToken ct) =>
            Results.Ok(await service.SummaryAsync(projectId, ct))).RequirePermission(Permissions.TimeClockReview);

        clock.MapPost("/days/confirm", async (ConfirmTimeClockDaysRequest request, TimeClockReviewService service, CancellationToken ct) =>
            Results.Ok(await service.ConfirmAsync(request, ct))).RequirePermission(Permissions.TimeClockReview);

        clock.MapPost("/punches/manual", async (AddManualPunchRequest request, TimeClockPunchService service, CancellationToken ct) =>
        {
            var added = await service.AddManualAsync(request, ct);
            return Results.Created($"/api/timeclock/days?projectId={request.ProjectId}", added);
        }).RequirePermission(Permissions.TimeClockReview);

        clock.MapPost("/punches/{id:guid}/void", async (Guid id, VoidPunchRequest request, TimeClockPunchService service, CancellationToken ct) =>
            Results.Ok(await service.VoidAsync(id, request, ct))).RequirePermission(Permissions.TimeClockReview);

        // ---- Imports, badge codes and badges (timeclock.manage) ----
        // The file's text is sent in the body ({ "csv": "..." }). It is read in memory and never stored.
        clock.MapPost("/import", async ([FromQuery] Guid projectId, ImportPunchesRequest request, TimeClockPunchService service, CancellationToken ct) =>
            Results.Ok(await service.ImportAsync(projectId, request.Csv, ct)))
            .RequirePermission(Permissions.TimeClockManage).WithMetadata(new RequestSizeLimitAttribute(MaximumImportBytes));

        clock.MapGet("/badges", async ([FromQuery] Guid[]? workerIds, TimeClockBadgeService service, CancellationToken ct) =>
            Results.Ok(await service.BadgesAsync(workerIds ?? [], ct))).RequirePermission(Permissions.TimeClockManage);

        clock.MapPost("/badges/issue-missing", async (TimeClockBadgeService service, CancellationToken ct) =>
            Results.Ok(await service.IssueMissingAsync(ct))).RequirePermission(Permissions.TimeClockManage);

        api.MapFeature("/workers/{id:guid}/punch-code", "Time clock").MapPost("", async (Guid id, IssuePunchCodeRequest request,
                TimeClockBadgeService service, CancellationToken ct) => Results.Ok(await service.IssueAsync(id, request, ct)))
            .RequirePermission(Permissions.TimeClockManage);

        // ---- Machines (settings.manage) ----
        var devices = clock.MapGroup("/devices");
        devices.MapGet("", async (TimeClockDeviceService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)))
            .RequirePermission(Permissions.SettingsManage);

        devices.MapPost("", async (RegisterDeviceRequest request, TimeClockDeviceService service, CancellationToken ct) =>
        {
            var registered = await service.RegisterAsync(request, ct);
            return Results.Created($"/api/timeclock/devices/{registered.Device.Id}", registered);
        }).RequirePermission(Permissions.SettingsManage);

        devices.MapPost("/{id:guid}/rotate-key", async (Guid id, VersionRequest request, TimeClockDeviceService service, CancellationToken ct) =>
            Results.Ok(await service.RotateKeyAsync(id, request, ct))).RequirePermission(Permissions.SettingsManage);

        devices.MapPost("/{id:guid}/revoke", async (Guid id, VersionRequest request, TimeClockDeviceService service, CancellationToken ct) =>
            Results.Ok(await service.RevokeAsync(id, request, ct))).RequirePermission(Permissions.SettingsManage);
    }
}

/// <summary>
/// The one route a time clock machine calls: <c>POST /api/timeclock/punches</c>. It is authenticated by the device key (the device scheme and policy),
/// never by a person's cookie, so it carries no anti-forgery token; it is rate limited per device; and the company comes only from the key.
/// See docs/api/timeclock.md for the contract.
/// </summary>
public static class TimeClockMachineEndpoints
{
    public const string PunchesRoute = "/timeclock/punches";

    public static void Map(RouteGroupBuilder api) =>
        api.MapPost(PunchesRoute, async (PushPunchesRequest request, HttpContext http, TimeClockPunchService service, CancellationToken ct) =>
                Results.Ok(await service.PushAsync(DeviceKeyAuthentication.DeviceId(http.User), request, ct)))
            .RequireAuthorization(DeviceKeyAuthentication.Policy)
            .RequireRateLimiting(DeviceKeyAuthentication.RateLimitPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(256 * 1024))
            .WithTags("Time clock");
}
