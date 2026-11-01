using Cms.Api.Security;
using Cms.Application.Common;
using Cms.Application.Features.Audit;
using Cms.Application.Features.Identity;
using Cms.Application.Features.Organizations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Cms.Api.Endpoints;

public sealed class UserEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/users", "Users");

        group.MapGet("", async (UserAdminService users, CancellationToken ct) => Results.Ok(await users.ListAsync(ct)))
            .RequirePermission(Permissions.UsersManage);

        group.MapGet("/{id:guid}", async (Guid id, UserAdminService users, CancellationToken ct) => Results.Ok(await users.GetAsync(id, ct)))
            .RequirePermission(Permissions.UsersManage);

        group.MapPost("", async (CreateUserRequest request, UserAdminService users, CancellationToken ct) =>
        {
            var created = await users.CreateAsync(request, ct);
            return Results.Created($"/api/users/{created.Id}", created);
        }).RequirePermission(Permissions.UsersManage);

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, UserAdminService users, IMemoryCache cache, CancellationToken ct) =>
        {
            var updated = await users.UpdateAsync(id, request, ct);
            SessionValidator.Forget(cache, updated.Username);
            return Results.Ok(updated);
        }).RequirePermission(Permissions.UsersManage);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, UserAdminService users, IMemoryCache cache, CancellationToken ct) =>
        {
            var updated = await users.DeactivateAsync(id, ct);
            SessionValidator.Forget(cache, updated.Username);
            return Results.Ok(updated);
        }).RequirePermission(Permissions.UsersManage);

        group.MapPost("/{id:guid}/reactivate", async (Guid id, UserAdminService users, CancellationToken ct) => Results.Ok(await users.ReactivateAsync(id, ct)))
            .RequirePermission(Permissions.UsersManage);

        group.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest request, UserAdminService users, IMemoryCache cache, CancellationToken ct) =>
        {
            await users.ResetPasswordAsync(id, request, ct);
            var user = await users.GetAsync(id, ct);
            SessionValidator.Forget(cache, user.Username);
            return Results.NoContent();
        }).RequirePermission(Permissions.UsersManage);
    }
}

public sealed class OrganizationEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/organization", "Organization");

        group.MapGet("", async (OrganizationService service, CancellationToken ct) => Results.Ok(await service.GetAsync(ct)));

        group.MapPut("", async (UpdateOrganizationRequest request, OrganizationService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(request, ct))).RequirePermission(Permissions.SettingsManage);
    }
}

public sealed class AuditEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        api.MapFeature("/audit", "Audit").MapGet("", async ([FromQuery] string? search, [FromQuery] string? entityType, [FromQuery] Guid? entityId,
                [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [AsParameters] PageQuery page, AuditQueryService audit, CancellationToken ct) =>
            Results.Ok(await audit.ListAsync(new AuditQuery(search, entityType, entityId, from, to), page.ToRequest(), ct)))
            .RequirePermission(Permissions.AuditRead);
    }
}
