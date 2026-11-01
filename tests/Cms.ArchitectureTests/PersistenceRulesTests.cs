using Cms.Application.Common;
using Cms.Domain.Common;
using Cms.Domain.Features.Identity;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cms.ArchitectureTests;

/// <summary>Rules about the database model that every feature must keep, checked on the real EF model (no database needed).</summary>
public sealed class PersistenceRulesTests
{
    private static readonly Lazy<IModel> Model = new(() =>
    {
        var options = new DbContextOptionsBuilder<CmsDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options;
        using var db = new CmsDbContext(options, new NoOneSignedIn());
        return db.Model;
    });

    [Fact]
    public void Every_tenant_owned_entity_is_filtered_to_the_signed_in_organization()
    {
        var unfiltered = Model.Value.GetEntityTypes()
            .Where(type => type.FindProperty("TenantId") is not null && type.BaseType is null)
            .Where(type => !type.GetDeclaredQueryFilters().Any())
            .Select(type => type.ClrType.Name).ToArray();

        Assert.Empty(unfiltered);
    }

    [Fact]
    public void Every_entity_belongs_to_an_organization()
    {
        var withoutTenant = Model.Value.GetEntityTypes().Where(type => type.FindProperty("TenantId") is null).Select(type => type.ClrType.Name).ToArray();

        Assert.Empty(withoutTenant);
    }

    [Fact]
    public void Every_aggregate_has_a_concurrency_token()
    {
        var missing = Model.Value.GetEntityTypes()
            .Where(type => typeof(AggregateRoot).IsAssignableFrom(type.ClrType))
            .Where(type => type.FindProperty(nameof(AggregateRoot.Version))?.IsConcurrencyToken != true)
            .Select(type => type.ClrType.Name).ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Money_is_stored_in_minor_units_and_never_as_a_floating_point_column()
    {
        var offenders = new List<string>();
        foreach (var type in Model.Value.GetEntityTypes())
        foreach (var property in type.GetProperties())
        {
            var name = property.Name;
            var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (clr == typeof(double) || clr == typeof(float)) offenders.Add($"{type.ClrType.Name}.{name} is floating point");
            if (name.EndsWith("Minor", StringComparison.Ordinal) && clr != typeof(long)) offenders.Add($"{type.ClrType.Name}.{name} should be long");
            if (clr == typeof(decimal) && (name.Contains("Amount") || name.Contains("Price") || name.Contains("Cost") || name.Contains("Pay") || name.Contains("Total")))
                offenders.Add($"{type.ClrType.Name}.{name} looks like money but is decimal");
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Dates_are_stored_as_dates_never_as_text()
    {
        var offenders = Model.Value.GetEntityTypes()
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .Where(pair => pair.property.ClrType == typeof(string) &&
                (pair.property.Name.EndsWith("Date", StringComparison.Ordinal) || pair.property.Name.EndsWith("At", StringComparison.Ordinal)))
            .Select(pair => $"{pair.type.ClrType.Name}.{pair.property.Name}").ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_string_column_has_an_explicit_maximum_length()
    {
        var offenders = Model.Value.GetEntityTypes()
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .Where(pair => pair.property.ClrType == typeof(string) && pair.property.GetMaxLength() is null)
            .Select(pair => $"{pair.type.ClrType.Name}.{pair.property.Name}").ToArray();

        Assert.Empty(offenders);
    }

    private sealed class NoOneSignedIn : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid UserId => Guid.Empty;
        public Guid TenantId => Guid.Empty;
        public string UserName => "";
        public string DisplayName => "";
        public AccessRole Roles => AccessRole.None;
        public string? CorrelationId => null;
    }
}
