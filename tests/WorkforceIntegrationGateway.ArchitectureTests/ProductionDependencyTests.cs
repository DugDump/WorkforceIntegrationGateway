using System.Xml.Linq;

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
