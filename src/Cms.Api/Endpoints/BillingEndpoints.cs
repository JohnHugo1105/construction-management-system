using Cms.Application.Common;
using Cms.Application.Features.Billing;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Endpoints;

/// <summary>
/// Client billing: a project's billing position and terms, bills to the client, and the payments recorded against them.
/// Reading needs billing.read; preparing bills, saving terms and recording payments need billing.manage; issuing and voiding bills,
/// and voiding payments, need billing.approve. Nothing here sends anything to a client or collects money.
/// </summary>
public sealed class BillingEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var inProject = api.MapFeature("/projects/{projectId:guid}/billing", "Billing");

        inProject.MapGet("", async (Guid projectId, [AsParameters] PageQuery page, ProjectBillingService billing, CancellationToken ct) =>
            Results.Ok(await billing.GetAsync(projectId, page.ToRequest(), ct))).RequirePermission(Permissions.BillingRead);

        inProject.MapPut("/terms", async (Guid projectId, SaveBillingTermsRequest request, ProjectBillingService billing, CancellationToken ct) =>
            Results.Ok(await billing.SaveTermsAsync(projectId, request, ct))).RequirePermission(Permissions.BillingManage);

        // The breakdown of a progress bill before it is saved, worked out by the same rules as saving it. Nothing is stored.
        inProject.MapGet("/preview", async (Guid projectId, [FromQuery] long cumulativeWorkMinor, [FromQuery] DateOnly? billingDate,
                ProjectBillingService billing, CancellationToken ct) =>
            Results.Ok(await billing.PreviewAsync(projectId, cumulativeWorkMinor, billingDate, ct))).RequirePermission(Permissions.BillingManage);

        inProject.MapPost("/invoices", async (Guid projectId, CreateClientInvoiceRequest request, ClientInvoiceService invoices, CancellationToken ct) =>
        {
            var created = await invoices.CreateAsync(projectId, request, ct);
            return Results.Created($"/api/client-invoices/{created.Id}", created);
        }).RequirePermission(Permissions.BillingManage);

        api.MapFeature("/billing", "Billing").MapGet("/receivables", async ([AsParameters] PageQuery page, ProjectBillingService billing, CancellationToken ct) =>
            Results.Ok(await billing.GetReceivablesAsync(page.ToRequest(), ct))).RequirePermission(Permissions.BillingRead);

        var bills = api.MapFeature("/client-invoices", "Billing");

        bills.MapGet("", async ([FromQuery] Guid? projectId, [FromQuery] string? status, [FromQuery] bool? overdue, [FromQuery] string? search,
                [AsParameters] PageQuery page, ClientInvoiceService invoices, CancellationToken ct) =>
            Results.Ok(await invoices.ListAsync(new ClientInvoiceListQuery(projectId, status, overdue ?? false, search), page.ToRequest(), ct)))
            .RequirePermission(Permissions.BillingRead);

        bills.MapGet("/{id:guid}", async (Guid id, ClientInvoiceService invoices, CancellationToken ct) => Results.Ok(await invoices.GetAsync(id, ct)))
            .RequirePermission(Permissions.BillingRead);

        bills.MapPut("/{id:guid}", async (Guid id, UpdateClientInvoiceRequest request, ClientInvoiceService invoices, CancellationToken ct) =>
            Results.Ok(await invoices.UpdateAsync(id, request, ct))).RequirePermission(Permissions.BillingManage);

        bills.MapPost("/{id:guid}/discard", async (Guid id, BillingReasonRequest request, ClientInvoiceService invoices, CancellationToken ct) =>
            Results.Ok(await invoices.DiscardAsync(id, request, ct))).RequirePermission(Permissions.BillingManage);

        bills.MapPost("/{id:guid}/issue", async (Guid id, VersionRequest request, ClientInvoiceService invoices, CancellationToken ct) =>
            Results.Ok(await invoices.IssueAsync(id, request, ct))).RequirePermission(Permissions.BillingApprove);

        bills.MapPost("/{id:guid}/void", async (Guid id, BillingReasonRequest request, ClientInvoiceService invoices, CancellationToken ct) =>
            Results.Ok(await invoices.VoidAsync(id, request, ct))).RequirePermission(Permissions.BillingApprove);

        bills.MapPost("/{id:guid}/payments", async (Guid id, RecordClientPaymentRequest request, ClientInvoiceService invoices, CancellationToken ct) =>
        {
            var paid = await invoices.RecordPaymentAsync(id, request, ct);
            return Results.Created($"/api/client-invoices/{paid.Id}", paid);
        }).RequirePermission(Permissions.BillingManage);

        bills.MapPost("/{id:guid}/payments/{paymentId:guid}/void", async (Guid id, Guid paymentId, BillingReasonRequest request, ClientInvoiceService invoices,
                CancellationToken ct) =>
            Results.Ok(await invoices.VoidPaymentAsync(id, paymentId, request, ct))).RequirePermission(Permissions.BillingApprove);
    }
}
