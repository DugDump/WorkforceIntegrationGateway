using System.Xml.Linq;

namespace WorkforceIntegrationGateway.ArchitectureTests;

internal static class ProjectDependencyRules
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedReferences =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["WorkforceIntegrationGateway.Domain"] = new HashSet<string>(StringComparer.Ordinal),
            ["WorkforceIntegrationGateway.Application"] = new HashSet<string>(StringComparer.Ordinal)
            {
                "WorkforceIntegrationGateway.Domain"
            },
            ["WorkforceIntegrationGateway.Infrastructure"] = new HashSet<string>(StringComparer.Ordinal)
            {
                "WorkforceIntegrationGateway.Application",
                "WorkforceIntegrationGateway.Domain"
            },
            ["WorkforceIntegrationGateway.Api"] = new HashSet<string>(StringComparer.Ordinal)
            {
                "WorkforceIntegrationGateway.Application",
                "WorkforceIntegrationGateway.Infrastructure"
            }
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedPackages =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["WorkforceIntegrationGateway.Domain"] = new HashSet<string>(StringComparer.Ordinal),
            ["WorkforceIntegrationGateway.Application"] = new HashSet<string>(StringComparer.Ordinal),
            ["WorkforceIntegrationGateway.Infrastructure"] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.Design",
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                "Npgsql"
            },
            ["WorkforceIntegrationGateway.Api"] = new HashSet<string>(StringComparer.Ordinal)
            {
                "Microsoft.AspNetCore.OpenApi"
            }
        };

    internal static IReadOnlyList<string> FindViolations(string projectName, XDocument project)
    {
        var allowed = AllowedReferences[projectName];

        return project
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")?.Value))
            .Where(reference => reference is not null && !allowed.Contains(reference))
            .Select(reference => $"{projectName} must not reference {reference}.")
            .ToArray();
    }

    internal static IReadOnlyList<string> FindPackageViolations(string projectName, XDocument project)
    {
        var allowed = AllowedPackages[projectName];

        return project
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(package => package is not null && !allowed.Contains(package))
            .Select(package => $"{projectName} must not reference package {package}.")
            .ToArray();
    }
}
