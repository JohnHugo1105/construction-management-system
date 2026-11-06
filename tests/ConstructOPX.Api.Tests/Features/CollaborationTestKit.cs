using System.Text.Json;
using System.Text.RegularExpressions;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Billing;
using ConstructOPX.Application.Features.ChangeOrders;
using ConstructOPX.Application.Features.Collaboration;
using ConstructOPX.Application.Features.Costing;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Application.Features.Subcontracts;
using ConstructOPX.Domain.Features.Identity;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Request builders and a company with one record of every kind people discuss, shared by the collaboration API tests.</summary>
internal static class CollaborationTestKit
{
    /// <summary>A currency sign, or a figure with thousands separators or two decimals: what an amount, a rate or a pay figure looks like.</summary>
    public static readonly Regex MoneyLike = new(@"[₱$€£]|PHP|\d{1,3}(,\d{3})+|\d+\.\d{2}\b", RegexOptions.CultureInvariant);

    /// <summary>A signed-in Administrator's company with a project, a draft purchase order, a draft change order, a draft bill and an awarded subcontract.</summary>
    public sealed record World(ApiClient Admin, SeededUser User, ProjectDto Project, PurchaseOrderDto Order, ChangeOrderDto ChangeOrder,
        ClientInvoiceDto Bill, SubcontractDto Subcontract)
    {
        /// <summary>The id of the record of a kind ("Project", "PurchaseOrder", ...).</summary>
        public Guid IdOf(string kind) => kind switch
        {
            "Project" => Project.Id,
            "PurchaseOrder" => Order.Id,
            "ChangeOrder" => ChangeOrder.Id,
            "ClientBill" => Bill.Id,
            "Subcontract" => Subcontract.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static async Task<World> WorldAsync(ConstructOpxApiFactory factory)
    {
        var (admin, user) = await SignedInAsync(factory);
        var project = await CreateProjectAsync(admin);
        var firm = await PurchasingTestKit.CreateFirmAsync(admin);
        var order = await PurchasingTestKit.DraftAsync(admin, project, firm);
        var changeOrder = await DraftChangeOrderAsync(admin, project);
        var bill = await DraftBillAsync(admin, project.Id);
        var subcontract = await SubcontractTestKit.AwardedAsync(admin, project.Id);
        return new World(admin, user, await admin.GetAsync<ProjectDto>($"/api/projects/{project.Id}"), order, changeOrder, bill, subcontract);
    }

    public static async Task<(ApiClient Client, SeededUser User)> AsRoleAsync(ConstructOpxApiFactory factory, World world, AccessRole role) =>
        await SignedInAsync(factory, role, world.User.TenantId);

    public static async Task<ChangeOrderDto> DraftChangeOrderAsync(ApiClient client, ProjectDto project)
    {
        await client.PostAsync<BudgetLineDto>($"/api/projects/{project.Id}/budget-lines", Line("Ready-mix concrete", unitCostMinor: 50_000));
        var current = await client.GetAsync<ProjectDto>($"/api/projects/{project.Id}");
        await client.PostAsync<BudgetBaselineDto>($"/api/projects/{project.Id}/budget-baseline/lock", new { version = current.Version });
        return await client.PostAsync<ChangeOrderDto>($"/api/projects/{project.Id}/change-orders",
            new { title = "Extra waterproofing", description = "The client asked for a membrane.", reason = "ClientRequest", contractPriceDeltaMinor = 0 });
    }

    public static async Task<ChangeOrderDto> SubmittedChangeOrderAsync(ApiClient client, ChangeOrderDto order)
    {
        order = await client.PostAsync<ChangeOrderDto>($"/api/change-orders/{order.Id}/lines", new
        {
            kind = "Add", description = "Waterproofing membrane", itemType = "Material", quantity = 3m, unit = "roll", unitCostMinor = 33_333,
            vatTreatment = "Inclusive", entryDate = "2026-02-01", version = order.Version
        });
        return await client.PostAsync<ChangeOrderDto>($"/api/change-orders/{order.Id}/submit", new { version = order.Version });
    }

    public static Task<ClientInvoiceDto> DraftBillAsync(ApiClient client, Guid projectId, long cumulativeMinor = 100_000_00) =>
        client.PostAsync<ClientInvoiceDto>($"/api/projects/{projectId}/billing/invoices",
            new { kind = "Progress", billingDate = "2026-02-01", description = "Progress billing", cumulativeWorkMinor = cumulativeMinor });

    // ---- Tasks ------------------------------------------------------------------------------------------------------------------

    public static Dictionary<string, object?> NewTask(Guid assigneeUserId, string? title = "Check the rebar quantities", Guid? projectId = null,
        string? dueDate = null, string? linkKind = null, Guid? linkId = null, string? clientRequestId = null, string? description = null) => new()
    {
        ["title"] = title, ["description"] = description, ["dueDate"] = dueDate, ["assigneeUserId"] = assigneeUserId, ["projectId"] = projectId,
        ["linkKind"] = linkKind, ["linkId"] = linkId, ["clientRequestId"] = clientRequestId
    };

    public static Task<WorkTaskDto> CreateTaskAsync(ApiClient client, Guid assigneeUserId, Guid? projectId = null, string? dueDate = null) =>
        client.PostAsync<WorkTaskDto>("/api/tasks", NewTask(assigneeUserId, projectId: projectId, dueDate: dueDate));

    public static Task<HttpResponseMessage> StepAsync(ApiClient client, WorkTaskDto task, string step, object? extra = null) =>
        client.PostAsync($"/api/tasks/{task.Id}/{step}", extra ?? new { version = task.Version });

    // ---- Comments ---------------------------------------------------------------------------------------------------------------

    public static Dictionary<string, object?> NewComment(string kind, Guid targetId, string? text = "Can the pour move to Monday?",
        IEnumerable<Guid>? mentions = null, string? clientRequestId = null) => new()
    {
        ["kind"] = kind, ["targetId"] = targetId, ["text"] = text, ["mentionUserIds"] = (mentions ?? []).ToArray(), ["clientRequestId"] = clientRequestId
    };

    public static Task<HttpResponseMessage> CommentAsync(ApiClient client, string kind, Guid targetId, IEnumerable<Guid>? mentions = null,
        string? text = "Can the pour move to Monday?") =>
        client.PostAsync("/api/comments", NewComment(kind, targetId, text, mentions));

    public static string Thread(string kind, Guid targetId, int pageSize = 25) => $"/api/comments?kind={kind}&targetId={targetId}&pageSize={pageSize}";

    public static Task<HttpResponseMessage> RemoveAsync(ApiClient client, CommentDto comment, string? reason = "Posted on the wrong record", int? version = null) =>
        client.PostAsync($"/api/comments/{comment.Id}/void", new { reason, version = version ?? comment.Version });

    // ---- Notifications ----------------------------------------------------------------------------------------------------------

    public static async Task<IReadOnlyList<NotificationDto>> NotificationsAsync(ApiClient client) =>
        (await client.GetAsync<PagedResult<NotificationDto>>("/api/notifications?pageSize=100")).Items;

    /// <summary>Every property name anywhere in a JSON document.</summary>
    public static List<string> PropertyNames(JsonElement element)
    {
        var names = new List<string>();
        void Walk(JsonElement item)
        {
            if (item.ValueKind == JsonValueKind.Object)
                foreach (var property in item.EnumerateObject())
                {
                    names.Add(property.Name);
                    Walk(property.Value);
                }
            else if (item.ValueKind == JsonValueKind.Array)
                foreach (var child in item.EnumerateArray()) Walk(child);
        }
        Walk(element);
        return names;
    }
}
