using System.Diagnostics;
using Xunit;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Uses E2E_APP_URL when supplied, otherwise launches the Blazor WASM dev server once per
/// collection. Polls readiness and disposes only the process it starts.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private Process? _app;

    public async ValueTask InitializeAsync()
    {
        if (!E2EEnvironment.IsAvailable)
            return; // Tests will Assert.Skip; don't burn 30s launching an app nobody will use.

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("E2E_APP_URL")))
        {
            _app = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project src/TheShop.Web --launch-profile http",
                WorkingDirectory = E2EEnvironment.RepoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }) ?? throw new InvalidOperationException("Failed to start the app process.");
        }

        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            if (_app?.HasExited == true)
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
