using System.IO;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// One way to read UI source text from a test. The wave suites each carried their own copy of the
/// repository-root walk and the path join; both live here so a move in the UI tree is fixed once.
/// </summary>
internal static class UiSourceFixture
{
    public static string RepositoryRoot()
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

        throw new InvalidOperationException($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
    }

    public static string ReadUi(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", Path.Combine(parts)));
}
