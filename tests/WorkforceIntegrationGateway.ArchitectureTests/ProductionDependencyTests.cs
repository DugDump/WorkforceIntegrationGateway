using System.Xml.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace WorkforceIntegrationGateway.ArchitectureTests;

public sealed class ProductionDependencyTests
{
    public static TheoryData<string> ProductionProjects => new()
    {
        "WorkforceIntegrationGateway.Domain",
        "WorkforceIntegrationGateway.Application",
        "WorkforceIntegrationGateway.Infrastructure",
        "WorkforceIntegrationGateway.Api"
    };

    [Theory]
    [MemberData(nameof(ProductionProjects))]
    public void Production_project_references_follow_declared_direction(string projectName)
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "src", projectName, $"{projectName}.csproj");
        var violations = ProjectDependencyRules.FindViolations(projectName, XDocument.Load(projectPath));

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(ProductionProjects))]
    public void Production_package_references_follow_declared_boundaries(string projectName)
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "src", projectName, $"{projectName}.csproj");
        var violations = ProjectDependencyRules.FindPackageViolations(projectName, XDocument.Load(projectPath));

        Assert.Empty(violations);
    }

    [Fact]
    public void Prohibited_dependency_is_reported_with_an_understandable_message()
    {
        var prohibitedProject = XDocument.Parse(
            """
            <Project>
              <ItemGroup>
                <ProjectReference Include="..\WorkforceIntegrationGateway.Infrastructure\WorkforceIntegrationGateway.Infrastructure.csproj" />
              </ItemGroup>
            </Project>
            """);

        var violation = Assert.Single(
            ProjectDependencyRules.FindViolations("WorkforceIntegrationGateway.Domain", prohibitedProject));

        Assert.Equal(
            "WorkforceIntegrationGateway.Domain must not reference WorkforceIntegrationGateway.Infrastructure.",
            violation);
    }

    [Fact]
    public void Prohibited_domain_package_is_reported_with_an_understandable_message()
    {
        var prohibitedProject = XDocument.Parse(
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore" />
              </ItemGroup>
            </Project>
            """);

        var violation = Assert.Single(
            ProjectDependencyRules.FindPackageViolations("WorkforceIntegrationGateway.Domain", prohibitedProject));

        Assert.Equal(
            "WorkforceIntegrationGateway.Domain must not reference package Microsoft.EntityFrameworkCore.",
            violation);
    }

    [Fact]
    public void Domain_compiled_assembly_has_no_forbidden_framework_dependencies()
    {
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var targetFramework = testOutput.Name;
        var configuration = testOutput.Parent?.Name
            ?? throw new DirectoryNotFoundException("Could not determine the active build configuration.");
        var assemblyPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "WorkforceIntegrationGateway.Domain",
            "bin",
            configuration,
            targetFramework,
            "WorkforceIntegrationGateway.Domain.dll");
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        var references = metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .ToArray();
        var forbiddenPrefixes = new[]
        {
            "Azure.",
            "Microsoft.AspNetCore.",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "System.Net.Http"
        };

        Assert.DoesNotContain(
            references,
            reference => forbiddenPrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WorkforceIntegrationGateway.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the Workforce Integration Gateway repository root.");
    }
}
