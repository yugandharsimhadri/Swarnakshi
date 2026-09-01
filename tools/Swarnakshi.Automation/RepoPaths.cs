namespace Swarnakshi.Automation;

/// <summary>
/// Locates the repository layout at runtime. The UAT binaries sit several levels deep under bin/,
/// and a CI runner may launch them from anywhere, so nothing here relies on
/// Environment.CurrentDirectory — the root is found by walking up until the solution file appears.
/// </summary>
public static class RepoPaths
{
    private const string SolutionFileName = "Swarnakshi.slnx";

    private static readonly Lazy<string> RootLazy = new(FindRoot);

    /// <summary>Absolute path to the repository root (the folder holding Swarnakshi.slnx).</summary>
    public static string Root => RootLazy.Value;

    /// <summary>Absolute path to web/, the Vite + React client.</summary>
    public static string WebProject => Path.Combine(Root, "web");

    /// <summary>The API project, started fresh against a throwaway database for each run.</summary>
    public static string ApiProject => Path.Combine(Root, "src", "Swarnakshi.Api", "Swarnakshi.Api.csproj");

    /// <summary>Where screenshots and the throwaway database are written.</summary>
    public static string ArtifactsDir => Path.Combine(Root, "artifacts", "uat");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate '{SolutionFileName}' walking up from '{AppContext.BaseDirectory}'. " +
            "Set SWARNAKSHI_UAT_WEB_PATH to the absolute path of web/ to bypass repo discovery.");
    }
}
