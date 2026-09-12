using System.Diagnostics;
using Xunit;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Launches the already-built Blazor WASM dev server (<c>dotnet run --no-build --no-restore</c>) once per test
/// collection, polls <see cref="E2EEnvironment.AppBaseUrl"/> until it serves 200, and kills the
/// entire process tree on disposal so no orphaned process holds port 5218.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private Process? _app;

    public async ValueTask InitializeAsync()
    {
        if (!E2EEnvironment.IsAvailable)
            return; // Tests will Assert.Skip; don't burn 30s launching an app nobody will use.

        _app = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project src/TheShop.Web --launch-profile http --no-build --no-restore",
            WorkingDirectory = E2EEnvironment.RepoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Failed to start the app process.");

        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            if (_app.HasExited)
                throw new InvalidOperationException(
                    $"App process exited during startup:\n{await _app.StandardError.ReadToEndAsync()}");
            try
            {
                var res = await http.GetAsync(E2EEnvironment.AppBaseUrl);
                if (res.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { /* not listening yet */ }
            await Task.Delay(2000);
        }
        throw new TimeoutException($"App did not become ready at {E2EEnvironment.AppBaseUrl} within 120s.");
    }

    public ValueTask DisposeAsync()
    {
        if (_app is { HasExited: false })
            _app.Kill(entireProcessTree: true);
        _app?.Dispose();
        return ValueTask.CompletedTask;
    }
}
