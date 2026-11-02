using System.Net;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.SiteDiary;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.TestSupport;
using static ConstructOPX.Api.Tests.Features.CostingTestKit;
using static ConstructOPX.Api.Tests.Features.SiteDiaryTestKit;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>Who may read and who may write the site diary, route by route: every role, and a person who is not signed in (docs/development/notes/site-diary-rules.md).</summary>
public sealed class SiteDiaryAccessApiTests : IClassFixture<ConstructOpxApiFactory>
{
    private readonly ConstructOpxApiFactory _factory;

    public SiteDiaryAccessApiTests(ConstructOpxApiFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task Each_role_reads_and_writes_exactly_what_its_permission_allows()
    {
        var (admin, user) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        var entry = await CreateAsync(admin, project.Id, DaysFromToday(0), "Written by the administrator");

        // diary.read: Administrator, Project Manager, Cost Engineer, Accountant, Warehouse Keeper, Viewer. diary.write: the first three.
        var reads = new[] { AccessRole.Administrator, AccessRole.ProjectManager, AccessRole.CostEngineer, AccessRole.Accountant, AccessRole.WarehouseKeeper, AccessRole.Viewer };
        var writes = new[] { AccessRole.Administrator, AccessRole.ProjectManager, AccessRole.CostEngineer };
        var day = -1;

        foreach (var role in Enum.GetValues<AccessRole>().Where(role => role != AccessRole.None))
        {
            var (client, _) = await SignedInAsync(_factory, role, user.TenantId);
            using var _c = client;
            var mayRead = reads.Contains(role);
            var mayWrite = writes.Contains(role);
            var read = mayRead ? HttpStatusCode.OK : HttpStatusCode.Forbidden;

            Assert.Equal((role, read), (role, (await client.GetAsync(Route(project.Id))).StatusCode));
            Assert.Equal((role, read), (role, (await client.GetAsync(Route(project.Id, DaysFromToday(0)))).StatusCode));
            Assert.Equal((role, read), (role, (await client.GetAsync(Route(project.Id, DaysFromToday(0), "filled-in"))).StatusCode));

            var create = await client.PostAsync(Route(project.Id), NewEntry(DaysFromToday(day--)));
            Assert.Equal((role, mayWrite ? HttpStatusCode.Created : HttpStatusCode.Forbidden), (role, create.StatusCode));

            // The administrator and project manager may change anyone's report; the others, who are not its author, are refused either way.
            var correct = await CorrectAsync(client, entry, Change(entry, weather: "Cloudy"));
            var voidIt = await VoidAsync(client, entry, "Not mine to void", entry.Version);
            if (!mayWrite)
            {
                Assert.Equal((role, HttpStatusCode.Forbidden), (role, correct.StatusCode));
                Assert.Equal((role, HttpStatusCode.Forbidden), (role, voidIt.StatusCode));
            }
            else if (role == AccessRole.CostEngineer)
            {
                // A writer who did not write the report may not change it.
                Assert.Equal((role, HttpStatusCode.Conflict), (role, correct.StatusCode));
                Assert.Equal((role, HttpStatusCode.Conflict), (role, voidIt.StatusCode));
            }
        }

        // The refusals changed nothing; the Project Manager's and the Administrator's allowed corrections did not void the report.
        var after = await admin.GetAsync<DiaryEntryDto>(Route(project.Id, DaysFromToday(0)));
        Assert.Equal("Posted", after.Status);
    }

    [SqlServerFact]
    public async Task A_person_who_is_not_signed_in_is_refused_on_every_diary_route()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);
        using var stranger = new ApiClient(_factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync(Route(project.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync(Route(project.Id, DaysFromToday(0)))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync(Route(project.Id, DaysFromToday(0), "filled-in"))).StatusCode);
    }

    [SqlServerFact]
    public async Task A_date_that_is_not_a_date_is_refused_without_a_server_error()
    {
        var (admin, _) = await SignedInAsync(_factory);
        using var _a = admin;
        var project = await CreateProjectAsync(admin);

        var response = await admin.GetAsync(Route(project.Id, "not-a-date"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(Route(project.Id, DaysFromToday(0)))).StatusCode);
        _ = (typeof(PagedResult<>), typeof(DiaryListItemDto));
    }
}
