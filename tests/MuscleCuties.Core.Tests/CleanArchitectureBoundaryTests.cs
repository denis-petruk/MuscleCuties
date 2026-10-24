using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Programmatic verification of mandatory Clean Architecture boundaries and invariants from AGENTS.md:
/// 1. MuscleCuties.Core references zero outer assemblies/packages (UseCases, Infrastructure, PostgreSql, Server, UI, EntityFrameworkCore, Maui, AspNetCore, Sqlite).
/// 2. MuscleCuties.UseCases references only MuscleCuties.Core (zero Infrastructure, PostgreSql, Server, UI, EntityFrameworkCore, Maui, Sqlite).
/// 3. Zero SQLite references across Core and UseCases.
/// 4. Zero synchronous blocking calls (.Wait(), .Result, .GetAwaiter().GetResult()) in Core and UseCases.
/// 5. Zero direct "using MuscleCuties.Core.Domain" imports in UI ViewModels.
/// </summary>
public class CleanArchitectureBoundaryTests
{
    private static readonly string RepoRoot = FindRepositoryRoot();

    private static readonly string[] ProhibitedCoreOuterTokens =
    [
        "UseCases",
        "Infrastructure",
        "PostgreSql",
        "Server",
        "UI",
        "EntityFrameworkCore",
        "Maui",
        "AspNetCore",
        "Sqlite"
    ];

    private static readonly string[] ProhibitedUseCasesOuterTokens =
    [
        "Infrastructure",
        "PostgreSql",
        "Server",
        "UI",
        "EntityFrameworkCore",
        "Maui",
        "AspNetCore",
        "Sqlite"
    ];

    private static readonly Regex BlockingWaitRegex = new(@"\.Wait\s*\(", RegexOptions.Compiled);
    private static readonly Regex BlockingResultRegex = new(@"\.Result\b", RegexOptions.Compiled);
    private static readonly Regex BlockingGetAwaiterRegex = new(@"\.GetAwaiter\s*\(\)\s*\.GetResult\s*\(", RegexOptions.Compiled);
    private static readonly Regex CoreDomainUsingRegex = new(@"^\s*using\s+(?:static\s+)?MuscleCuties\.Core\.Domain(\.|\s*;)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex CoreAnyUsingRegex = new(@"^\s*using\s+(?:static\s+)?MuscleCuties\.Core(\.|\s*;)", RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void Core_assembly_and_project_reference_zero_outer_dependencies()
    {
        var coreAssembly = typeof(CyclePredictionEstimate).Assembly;
        var referencedAssemblies = coreAssembly.GetReferencedAssemblies();

        foreach (var referenced in referencedAssemblies)
        {
            var name = referenced.Name ?? string.Empty;
            foreach (var prohibited in ProhibitedCoreOuterTokens)
            {
                Assert.False(
                    name.Contains(prohibited, StringComparison.OrdinalIgnoreCase),
                    $"MuscleCuties.Core must not reference outer assembly '{name}' (matched prohibited token '{prohibited}').");
            }
        }

        var coreCsprojPath = Path.Combine(RepoRoot, "src", "MuscleCuties.Core", "MuscleCuties.Core.csproj");
        Assert.True(File.Exists(coreCsprojPath), $"Core project file not found at {coreCsprojPath}");

        var doc = XDocument.Load(coreCsprojPath);
        var projectReferences = doc.Descendants("ProjectReference").ToList();
        Assert.Empty(projectReferences);

        var packageReferences = doc.Descendants("PackageReference").ToList();
        foreach (var pkg in packageReferences)
        {
            var include = pkg.Attribute("Include")?.Value ?? string.Empty;
            foreach (var prohibited in ProhibitedCoreOuterTokens)
            {
                Assert.False(
                    include.Contains(prohibited, StringComparison.OrdinalIgnoreCase),
                    $"MuscleCuties.Core.csproj must not reference package '{include}' (matched prohibited token '{prohibited}').");
            }
        }
    }

    [Fact]
    public void UseCases_assembly_and_project_reference_only_Core()
    {
        var useCasesAssembly = typeof(CyclePhaseDto).Assembly;
        var referencedAssemblies = useCasesAssembly.GetReferencedAssemblies();

        foreach (var referenced in referencedAssemblies)
        {
            var name = referenced.Name ?? string.Empty;

            if (name.StartsWith("MuscleCuties.", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(
                    string.Equals(name, "MuscleCuties.Core", StringComparison.OrdinalIgnoreCase),
                    $"MuscleCuties.UseCases can only reference MuscleCuties.Core, but references '{name}'.");
            }

            foreach (var prohibited in ProhibitedUseCasesOuterTokens)
            {
                Assert.False(
                    name.Contains(prohibited, StringComparison.OrdinalIgnoreCase),
                    $"MuscleCuties.UseCases must not reference outer assembly '{name}' (matched prohibited token '{prohibited}').");
            }
        }

        var useCasesCsprojPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UseCases", "MuscleCuties.UseCases.csproj");
        Assert.True(File.Exists(useCasesCsprojPath), $"UseCases project file not found at {useCasesCsprojPath}");

        var doc = XDocument.Load(useCasesCsprojPath);
        var projectReferences = doc.Descendants("ProjectReference").ToList();
        foreach (var proj in projectReferences)
        {
            var include = proj.Attribute("Include")?.Value ?? string.Empty;
            Assert.True(
                include.EndsWith("MuscleCuties.Core.csproj", StringComparison.OrdinalIgnoreCase),
                $"MuscleCuties.UseCases.csproj can only reference MuscleCuties.Core, but found '{include}'.");
        }

        var packageReferences = doc.Descendants("PackageReference").ToList();
        foreach (var pkg in packageReferences)
        {
            var include = pkg.Attribute("Include")?.Value ?? string.Empty;
            foreach (var prohibited in ProhibitedUseCasesOuterTokens)
            {
                Assert.False(
                    include.Contains(prohibited, StringComparison.OrdinalIgnoreCase),
                    $"MuscleCuties.UseCases.csproj must not reference package '{include}' (matched prohibited token '{prohibited}').");
            }
        }
    }

    [Fact]
    public void Zero_sqlite_references_across_Core_and_UseCases()
    {
        var assemblies = new[]
        {
            typeof(CyclePredictionEstimate).Assembly,
            typeof(CyclePhaseDto).Assembly
        };

        foreach (var assembly in assemblies)
        {
            foreach (var referenced in assembly.GetReferencedAssemblies())
            {
                var name = referenced.Name ?? string.Empty;
                Assert.False(
                    name.Contains("sqlite", StringComparison.OrdinalIgnoreCase),
                    $"{assembly.GetName().Name} references SQLite assembly '{name}'. SQLite is strictly forbidden in Core/UseCases.");
            }
        }

        var csprojPaths = new[]
        {
            Path.Combine(RepoRoot, "src", "MuscleCuties.Core", "MuscleCuties.Core.csproj"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UseCases", "MuscleCuties.UseCases.csproj")
        };

        foreach (var csproj in csprojPaths)
        {
            var doc = XDocument.Load(csproj);
            var packages = doc.Descendants("PackageReference")
                .Select(e => e.Attribute("Include")?.Value ?? string.Empty);
            foreach (var pkg in packages)
            {
                Assert.False(
                    pkg.Contains("sqlite", StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(csproj)} contains PackageReference to '{pkg}'. SQLite is prohibited.");
            }
        }
    }

    [Fact]
    public void Zero_synchronous_blocking_calls_in_Core_and_UseCases()
    {
        var targetDirectories = new[]
        {
            Path.Combine(RepoRoot, "src", "MuscleCuties.Core"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UseCases")
        };

        var violations = new List<string>();

        foreach (var dir in targetDirectories)
        {
            var csFiles = Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));

            foreach (var file in csFiles)
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                        continue;

                    if (BlockingWaitRegex.IsMatch(line))
                    {
                        violations.Add($"{Path.GetRelativePath(RepoRoot, file)}:{i + 1} - Synchronous .Wait() call: {trimmed}");
                    }
                    if (BlockingResultRegex.IsMatch(line))
                    {
                        // Exclude common safe patterns such as ResultingTargetItems or Result<T> if any
                        violations.Add($"{Path.GetRelativePath(RepoRoot, file)}:{i + 1} - Synchronous .Result access: {trimmed}");
                    }
                    if (BlockingGetAwaiterRegex.IsMatch(line))
                    {
                        violations.Add($"{Path.GetRelativePath(RepoRoot, file)}:{i + 1} - Synchronous .GetAwaiter().GetResult() call: {trimmed}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} synchronous blocking call(s) in Core/UseCases:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Zero_Core_Domain_imports_in_UI_ViewModels()
    {
        var viewModelsDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels");
        if (!Directory.Exists(viewModelsDir))
            return;

        var csFiles = Directory.EnumerateFiles(viewModelsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                        !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var match = CoreDomainUsingRegex.Match(content);
            if (match.Success)
            {
                violations.Add($"{Path.GetRelativePath(RepoRoot, file)} has prohibited import: '{match.Value.Trim()}'");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} UI ViewModel(s) importing MuscleCuties.Core.Domain directly:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Zero_Core_imports_in_Workout_Cycle_Dashboard_and_Auth_ViewModels()
    {
        string[] migratedFeatureDirs = ["Workout", "Cycle", "Dashboard", "Auth"];
        var violations = new List<string>();

        foreach (var feature in migratedFeatureDirs)
        {
            var featureDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels", feature);
            if (!Directory.Exists(featureDir))
                continue;

            var csFiles = Directory.EnumerateFiles(featureDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));

            foreach (var file in csFiles)
            {
                var content = File.ReadAllText(file);
                var match = CoreAnyUsingRegex.Match(content);
                if (match.Success)
                {
                    violations.Add($"{Path.GetRelativePath(RepoRoot, file)} has prohibited import: '{match.Value.Trim()}'");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} migrated ViewModel(s) importing MuscleCuties.Core directly:\n" + string.Join("\n", violations));
    }

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "MuscleCuties.sln")) ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException($"Could not locate repository root starting from '{AppContext.BaseDirectory}'.");
    }
}
