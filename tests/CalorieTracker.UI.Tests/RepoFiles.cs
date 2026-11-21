using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests;

/// <summary>Source files in the repo, found from the test output folder.</summary>
internal static partial class RepoFiles
{
    /// <summary>Repo root: the folder that holds CalorieTracker.slnx.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>Full path of a file or folder under the repo root.</summary>
    public static string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Text of a file under the repo root.</summary>
    public static string Read(params string[] parts) => File.ReadAllText(PathOf(parts));

    /// <summary>Files under a repo folder matching a pattern, excluding bin and obj.</summary>
    public static IEnumerable<string> Find(string pattern, params string[] folder) =>
        Directory.EnumerateFiles(PathOf(folder), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"));

    /// <summary>Body of the first rule whose selector is exactly <c>.className</c> (alone or after another rule, a comma or space), without the braces; fails the test when there is none.</summary>
    public static string Rule(string css, string className)
    {
        var match = Regex.Match(css, @"(?:^|[},\s])\." + Regex.Escape(className) + @"\s*\{(?<body>[^}]*)\}");
        Assert.That(match.Success, Is.True, $"no .{className} rule");
        return match.Groups["body"].Value;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "CalorieTracker.slnx")))
        {
            directory = directory.Parent!;
        }

        return directory.FullName;
    }
}
