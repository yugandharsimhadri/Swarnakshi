using System.Text.Json;

namespace Swarnakshi.Tests;

/// <summary>
/// Where the tests find their PostgreSQL server: <c>testsettings.json</c> at the repository root.
///
/// <para>A file, not an environment variable, for the same reason the application's own settings
/// are a file: it is one thing to look at, it survives a new terminal, and it is the same on every
/// machine that has copied the template. The environment variable is still honoured, for a build
/// agent that has no repository root to speak of.</para>
///
/// <para>The file is git-ignored because it holds a password. The template beside it is what is
/// committed.</para>
/// </summary>
public static class TestSettings
{
    public const string FileName = "testsettings.json";
    private const string EnvironmentVariable = "SWARNAKSHI_TEST_POSTGRES";

    private static readonly Lazy<string> Loaded = new(Load);

    /// <summary>Connection string for the server, without a database.</summary>
    public static string Postgres => Loaded.Value;

    private static string Load()
    {
        var file = FindUpwards(FileName);
        if (file is not null)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (doc.RootElement.TryGetProperty("Postgres", out var value) && value.GetString() is { Length: > 0 } cs)
                return cs;
            throw new InvalidOperationException($"{file} has no \"Postgres\" connection string.");
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment;

        throw new InvalidOperationException(
            $"No {FileName} found in this folder or any parent, and {EnvironmentVariable} is not set. "
            + "Copy testsettings.template.json to testsettings.json at the repository root and fill in the "
            + "password. See docs/11-postgresql.md, 'Running the tests'.");
    }

    /// <summary>
    /// Walks up from the test assembly's folder until it finds the file. The assembly runs from
    /// bin/Debug/net10.0 four levels below the repository root, and hard-coding that depth is the
    /// kind of thing that breaks the first time somebody moves a project.
    /// </summary>
    public static string? FindUpwards(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
