using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

public sealed class ProjectEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var group = api.MapFeature("/projects", "Projects");

        group.MapGet("", async ([FromQuery] string? search, [FromQuery] string? status, [FromQuery] bool? includeArchived, [AsParameters] PageQuery page,
                ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.ListAsync(new ProjectListQuery(search, status, includeArchived ?? false), page.ToRequest(), ct)))
            .RequirePermission(Permissions.ProjectsRead);

        group.MapGet("/lookup", async ([FromQuery] bool? includeArchived, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.LookupAsync(includeArchived ?? false, ct))).RequirePermission(Permissions.ProjectsRead);

        group.MapGet("/{id:guid}", async (Guid id, ProjectService projects, CancellationToken ct) => Results.Ok(await projects.GetAsync(id, ct)))
            .RequirePermission(Permissions.ProjectsRead);

        group.MapPost("", async (CreateProjectRequest request, ProjectService projects, CancellationToken ct) =>
        {
            var created = await projects.CreateAsync(request, ct);
            return Results.Created($"/api/projects/{created.Id}", created);
        }).RequirePermission(Permissions.ProjectsManage);

        group.MapPut("/{id:guid}", async (Guid id, UpdateProjectRequest request, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.UpdateAsync(id, request, ct))).RequirePermission(Permissions.ProjectsManage);

        group.MapPost("/{id:guid}/start", async (Guid id, VersionRequest request, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.StartAsync(id, request, ct))).RequirePermission(Permissions.ProjectsManage);
        group.MapPost("/{id:guid}/hold", async (Guid id, VersionRequest request, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.PutOnHoldAsync(id, request, ct))).RequirePermission(Permissions.ProjectsManage);
        group.MapPost("/{id:guid}/resume", async (Guid id, VersionRequest request, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.ResumeAsync(id, request, ct))).RequirePermission(Permissions.ProjectsManage);
        group.MapPost("/{id:guid}/complete", async (Guid id, VersionRequest request, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.CompleteAsync(id, request, ct))).RequirePermission(Permissions.ProjectsManage);
        group.MapPost("/{id:guid}/reopen", async (Guid id, VersionRequest request, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.ReopenAsync(id, request, ct))).RequirePermission(Permissions.ProjectsManage);
        group.MapPost("/{id:guid}/archive", async (Guid id, ArchiveProjectRequest request, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.ArchiveAsync(id, request, ct))).RequirePermission(Permissions.ProjectsManage);
        group.MapPost("/{id:guid}/restore", async (Guid id, VersionRequest request, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.RestoreAsync(id, request, ct))).RequirePermission(Permissions.ProjectsManage);

        group.MapPost("/{id:guid}/sub-projects", async (Guid id, SaveSubProjectRequest request, ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.AddSubProjectAsync(id, request, ct))).RequirePermission(Permissions.ProjectsManage);
        group.MapPut("/{id:guid}/sub-projects/{subProjectId:guid}", async (Guid id, Guid subProjectId, SaveSubProjectRequest request,
                ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.UpdateSubProjectAsync(id, subProjectId, request, ct))).RequirePermission(Permissions.ProjectsManage);
        group.MapPost("/{id:guid}/sub-projects/{subProjectId:guid}/archive", async (Guid id, Guid subProjectId, VersionRequest request,
                ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.ArchiveSubProjectAsync(id, subProjectId, request, ct))).RequirePermission(Permissions.ProjectsManage);
        group.MapPost("/{id:guid}/sub-projects/{subProjectId:guid}/restore", async (Guid id, Guid subProjectId, VersionRequest request,
                ProjectService projects, CancellationToken ct) =>
            Results.Ok(await projects.RestoreSubProjectAsync(id, subProjectId, request, ct))).RequirePermission(Permissions.ProjectsManage);
    }
}
