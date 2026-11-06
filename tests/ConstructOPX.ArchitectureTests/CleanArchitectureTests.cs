using System.Xml.Linq;

namespace ConstructOPX.ArchitectureTests;

/// <summary>The dependency rule of Clean Architecture: dependencies always point inward.</summary>
public sealed class CleanArchitectureTests
{
    [Fact]
    public void Domain_has_no_project_references() => Assert.Empty(References("ConstructOPX.Domain"));

    [Fact]
    public void Application_depends_only_on_domain() => Assert.Equal(["ConstructOPX.Domain.csproj"], References("ConstructOPX.Application"));

    [Fact]
    public void Infrastructure_depends_only_on_inward_layers() =>
        Assert.Equal(["ConstructOPX.Application.csproj", "ConstructOPX.Domain.csproj"], References("ConstructOPX.Infrastructure"));

    [Fact]
    public void Api_is_the_only_composition_root() =>
        Assert.Equal(["ConstructOPX.Application.csproj", "ConstructOPX.Infrastructure.csproj"], References("ConstructOPX.Api"));

    [Fact]
    public void Inner_layers_have_no_package_references() =>
        Assert.All(new[] { "ConstructOPX.Domain", "ConstructOPX.Application" }, project =>
            Assert.Empty(XDocument.Load(ProjectFile(project)).Descendants("PackageReference")));

    [Fact]
    public void Inner_layers_do_not_use_framework_or_provider_namespaces()
    {
        foreach (var layer in new[] { "ConstructOPX.Domain", "ConstructOPX.Application" })
        foreach (var file in Repository.SourceFiles(layer))
        {
            var source = File.ReadAllText(file);
            foreach (var forbidden in new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Data.SqlClient", "System.Data.Common", "Microsoft.Extensions.DependencyInjection" })
                Assert.False(source.Contains(forbidden, StringComparison.Ordinal), $"{Path.GetRelativePath(Repository.Root, file)} uses {forbidden}.");
        }
    }

    [Fact]
    public void Money_and_quantities_are_never_floating_point_and_dates_are_never_text()
    {
        foreach (var layer in new[] { "ConstructOPX.Domain", "ConstructOPX.Application" })
        foreach (var file in Repository.SourceFiles(layer))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotMatch(@"\b(double|float)\b\??\s+\w+\s*[;{=,)]", source);
            // A const string is a name or a label (for example the "costs.read" permission), not a stored amount or date.
            Assert.DoesNotMatch(@"(?<!const\s)\bstring\??\s+\w*(Date|Amount|Total|Price|Cost)\w*\s*[;{=,)]", source);
        }
    }

    private static string[] References(string project) =>
        XDocument.Load(ProjectFile(project)).Descendants("ProjectReference")
            .Select(item => Path.GetFileName(((string)item.Attribute("Include")!).Replace('\\', '/'))).Order().ToArray();

    private static string ProjectFile(string project) => Path.Combine(Repository.Root, "src", project, $"{project}.csproj");
}
