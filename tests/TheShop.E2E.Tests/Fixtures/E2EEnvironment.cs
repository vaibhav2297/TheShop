namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Resolves the local E2E environment: repo root, app URL, and the Supabase keys exported by
/// <c>tools/start-e2e-env.ps1</c>. When the env file is absent, tests skip rather than fail.
/// </summary>
public static class E2EEnvironment
{
    /// <summary>Base URL of the Blazor WASM dev server used for E2E runs.</summary>
    public const string AppBaseUrl = "http://localhost:5218";

    /// <summary>Absolute path to the repository root, resolved from the test bin directory.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>Path to the env file written by <c>tools/start-e2e-env.ps1</c>.</summary>
    public static string EnvFilePath => Path.Combine(RepoRoot, "tests", "TheShop.E2E.Tests", ".e2e-env");

    /// <summary>Whether the local E2E environment has been started for this run.</summary>
    public static bool IsAvailable => File.Exists(EnvFilePath);

    /// <summary>Reads a KEY="value" line from .e2e-env; throws with a clear message if missing.</summary>
    public static string Get(string key)
    {
        var line = File.ReadAllLines(EnvFilePath)
            .FirstOrDefault(l => l.StartsWith(key + "=", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Key '{key}' not found in {EnvFilePath}. Re-run tools/start-e2e-env.ps1.");
        return line[(key.Length + 1)..].Trim('"');
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TheShop.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate TheShop.slnx above the test bin directory.");
    }
}
