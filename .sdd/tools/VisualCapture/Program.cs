using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    Process? app = null;
    try
    {
        var options = Options.Parse(arguments);
        if (options.Help)
        {
            Console.WriteLine("VisualCapture --url URL --output DIR --width N --height N --ready SELECTOR [--reference PNG] [--start-app] [--local-e2e] [--storage-state FILE] [--crop SELECTOR] [--click SELECTOR | --fill SELECTOR VALUE | --hover SELECTOR | --press SELECTOR KEY | --wait-for SELECTOR | --scroll SELECTOR]...");
            return 0;
        }

        if (options.StartApp)
        {
            var listener = new TcpListener(IPAddress.Loopback, 5218);
            try { listener.Start(); }
            finally { listener.Stop(); }
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = E2EEnvironment.RepoRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "run", "--project", "src/TheShop.Web", "--launch-profile", "http" })
                start.ArgumentList.Add(argument);
            app = Process.Start(start) ?? throw new InvalidOperationException("Could not launch app.");
            app.OutputDataReceived += (_, _) => { };
            app.ErrorDataReceived += (_, _) => { };
            app.BeginOutputReadLine();
            app.BeginErrorReadLine();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var deadline = DateTime.UtcNow.AddSeconds(120);
            var ready = false;
            while (DateTime.UtcNow < deadline)
            {
                if (app.HasExited) throw new InvalidOperationException($"App exited during startup ({app.ExitCode}). Run dotnet build for diagnostics.");
                try
                {
                    using var response = await http.GetAsync(E2EEnvironment.AppBaseUrl);
                    if (response.IsSuccessStatusCode) { ready = true; break; }
                }
                catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { }
                await Task.Delay(500);
            }
            if (!ready) throw new TimeoutException("App startup exceeded 120 seconds.");
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var contextOptions = new BrowserNewContextOptions
            {
                StorageStatePath = options.StorageState,
                DeviceScaleFactor = 1,
                Locale = "en-CA",
                TimezoneId = "UTC",
                ColorScheme = ColorScheme.Light,
                ReducedMotion = ReducedMotion.Reduce
            };
        await using var context = options.LocalE2E
            ? await ShopBrowser.NewContextAsync(browser, options.StorageState, contextOptions)
            : await browser.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(options.Width, options.Height);
        page.SetDefaultTimeout(30000);
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light, ReducedMotion = ReducedMotion.Reduce });
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        var responsePage = await page.GotoAsync(options.Url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        if (responsePage is { Ok: false }) throw new InvalidOperationException($"Page returned HTTP {responsePage.Status}.");
        try
        {
            await page.Locator(options.Ready).WaitForAsync(new() { State = WaitForSelectorState.Visible });
        }
        catch
        {
            Directory.CreateDirectory(options.Output);
            await page.ScreenshotAsync(new() { Path = Path.Combine(options.Output, "failure.png") });
            await File.WriteAllTextAsync(Path.Combine(options.Output, "failure.md"),
                $"# Capture readiness failure\n\nURL: {page.Url}\nSelector: {options.Ready}\n\n" + string.Join("\n", errors));
            throw;
        }
        foreach (var action in options.Actions)
        {
            var locator = page.Locator(action.Selector);
            switch (action.Kind)
            {
                case "--click": await locator.ClickAsync(); break;
                case "--fill": await locator.FillAsync(action.Value!); break;
                case "--hover": await locator.HoverAsync(); break;
                case "--press": await locator.PressAsync(action.Value!); break;
                case "--wait-for": await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible }); break;
                case "--scroll": await locator.ScrollIntoViewIfNeededAsync(); break;
            }
        }
        await page.AddStyleTagAsync(new() { Content = "*,*::before,*::after { animation:none!important; transition:none!important; caret-color:transparent!important; }" });
        await page.WaitForFunctionAsync("() => document.fonts.status === 'loaded'");
        await page.WaitForFunctionAsync("() => [...document.images].filter(i => { const r = i.getBoundingClientRect(); return r.width && r.height && r.bottom > 0 && r.top < innerHeight; }).every(i => i.complete && i.naturalWidth > 0)");

        byte[]? previous = null;
        byte[]? actual = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
            var current = options.Crop is null
                ? await page.ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled, Scale = ScreenshotScale.Css })
                : await page.Locator(options.Crop).ScreenshotAsync(new() { Animations = ScreenshotAnimations.Disabled, Scale = ScreenshotScale.Css });
            if (previous is not null && current.AsSpan().SequenceEqual(previous)) { actual = current; break; }
            previous = current;
        }
        if (actual is null) throw new InvalidOperationException("Screen did not stabilize in eight captures. Fix data, animation, or readiness before retrying.");
        if (errors.Count > 0) throw new InvalidOperationException("Browser page errors prevent capture: " + string.Join("; ", errors));

        var geometry = await page.Locator("[data-testid], button, input, h1, h2, h3, img").EvaluateAllAsync<string>("""
            elements => {
              const clean = s => String(s).replaceAll('|', '/').replaceAll('\n', ' ');
              return elements.map(el => {
                const r = el.getBoundingClientRect(), s = getComputedStyle(el);
                if (!r.width || !r.height) return null;
                return '| ' + [el.getAttribute('data-testid') || el.tagName, r.x, r.y, r.width, r.height,
                  s.fontFamily, s.fontSize, s.fontWeight, s.lineHeight, s.padding, s.gap, s.color, s.backgroundColor, s.objectFit].map(clean).join(' | ') + ' |';
              }).filter(Boolean).join('\n');
            }
            """);
        Directory.CreateDirectory(options.Output);
        await File.WriteAllBytesAsync(Path.Combine(options.Output, "actual.png"), actual);
        var report = new StringBuilder($"# Browser capture\n\n**URL:** {page.Url}\n**Viewport:** {options.Width}x{options.Height}\n**Browser:** Chromium {browser.Version}\n**OS:** {System.Runtime.InteropServices.RuntimeInformation.OSDescription}\n**Scale:** 1\n**Captured:** {DateTimeOffset.UtcNow:O}\n**Crop:** {options.Crop ?? "viewport"}\n**Verdict:** UNREVIEWED\n\n");
        if (options.Reference is not null)
        {
            var reference = await File.ReadAllBytesAsync(options.Reference);
            var comparison = await context.NewPageAsync();
            var result = await comparison.EvaluateAsync<JsonElement>("""
                async ({reference, actual}) => {
                  const load = async data => { const i = new Image(); i.src = 'data:image/png;base64,' + data; await i.decode(); return i; };
                  const a = await load(reference), b = await load(actual);
                  if (a.width !== b.width || a.height !== b.height) throw new Error(`Reference ${a.width}x${a.height} differs from capture ${b.width}x${b.height}. Export at 1x or correct crop; never stretch.`);
                  const canvas = document.createElement('canvas'); canvas.width = a.width; canvas.height = a.height;
                  const c = canvas.getContext('2d');
                  c.fillStyle = 'white'; c.fillRect(0,0,a.width,a.height); c.drawImage(a,0,0);
                  const x = c.getImageData(0,0,a.width,a.height);
                  c.fillStyle = 'white'; c.fillRect(0,0,a.width,a.height); c.drawImage(b,0,0);
                  const y = c.getImageData(0,0,a.width,a.height);
                  c.globalAlpha = 0.5; c.drawImage(a,0,0); c.globalAlpha = 1;
                  const overlay = canvas.toDataURL('image/png').split(',')[1];
                  let changed = 0;
                  const d = c.createImageData(a.width,a.height);
                  for(let i=0;i<x.data.length;i+=4) {
                    const delta = Math.max(...[0,1,2].map(k => Math.abs(x.data[i+k]-y.data[i+k])));
                    if(delta > 0) changed++;
                    d.data[i]=delta; d.data[i+1]=0; d.data[i+2]=delta; d.data[i+3]=255;
                  }
                  c.putImageData(d,0,0);
                  return { Overlay: overlay, Difference: canvas.toDataURL('image/png').split(',')[1], Changed: changed, Total: a.width*a.height };
                }
                """, new { reference = Convert.ToBase64String(reference), actual = Convert.ToBase64String(actual) });
            await File.WriteAllBytesAsync(Path.Combine(options.Output, "reference.png"), reference);
            await File.WriteAllBytesAsync(Path.Combine(options.Output, "overlay.png"), Convert.FromBase64String(result.GetProperty("Overlay").GetString()!));
            await File.WriteAllBytesAsync(Path.Combine(options.Output, "difference.png"), Convert.FromBase64String(result.GetProperty("Difference").GetString()!));
            report.AppendLine($"**Changed pixels:** {result.GetProperty("Changed").GetInt32()}/{result.GetProperty("Total").GetInt32()} (diagnostic only; no automatic fidelity verdict)\n");
        }
        report.AppendLine("| Element | X | Y | Width | Height | Font | Size | Weight | Line height | Padding | Gap | Color | Background | Object fit |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        report.AppendLine(geometry);
        await File.WriteAllTextAsync(Path.Combine(options.Output, "browser.md"), report.ToString());
        Console.WriteLine($"Captured {options.Output}. Images require visual review; no PASS assigned.");
        return 0;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine(error.Message);
        return 1;
    }
    finally
    {
        if (app is not null)
        {
            if (!app.HasExited) { app.Kill(entireProcessTree: true); await app.WaitForExitAsync(); }
            app.Dispose();
        }
    }
}

internal sealed record ActionStep(string Kind, string Selector, string? Value);

internal sealed class Options
{
    internal string Url = "";
    internal string Output = "";
    internal string Ready = "";
    internal string? Reference;
    internal string? Crop;
    internal string? StorageState;
    internal int Width;
    internal int Height;
    internal bool StartApp;
    internal bool LocalE2E;
    internal bool Help;
    internal List<ActionStep> Actions = [];

    internal static Options Parse(string[] args)
    {
        var result = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing option value.");
            var option = args[i];
            switch (option)
            {
                case "--help": result.Help = true; return result;
                case "--url": result.Url = Next(); break;
                case "--output": result.Output = Next(); break;
                case "--ready": result.Ready = Next(); break;
                case "--reference": result.Reference = Path.GetFullPath(Next()); break;
                case "--crop": result.Crop = Next(); break;
                case "--storage-state": result.StorageState = Path.GetFullPath(Next()); break;
                case "--width": result.Width = int.Parse(Next()); break;
                case "--height": result.Height = int.Parse(Next()); break;
                case "--start-app": result.StartApp = true; break;
                case "--local-e2e": result.LocalE2E = true; break;
                case "--click": case "--hover": case "--wait-for": case "--scroll":
                    result.Actions.Add(new(option, Next(), null)); break;
                case "--fill": case "--press": result.Actions.Add(new(option, Next(), Next())); break;
                default: throw new ArgumentException($"Unknown option {option}.");
            }
        }
        if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var url) || !url.IsLoopback || url.Scheme is not ("http" or "https"))
            throw new ArgumentException("Capture URL must be a local HTTP(S) address.");
        if (result.StartApp && url.GetLeftPart(UriPartial.Authority) != E2EEnvironment.AppBaseUrl)
            throw new ArgumentException("--start-app requires http://localhost:5218.");
        if (result.Width <= 0 || result.Height <= 0 || string.IsNullOrWhiteSpace(result.Ready) || string.IsNullOrWhiteSpace(result.Output))
            throw new ArgumentException("Supply positive --width/--height, --ready selector and --output directory.");
        if (result.StorageState is not null && !File.Exists(result.StorageState)) throw new FileNotFoundException("Storage state does not exist.");
        if (result.LocalE2E && !E2EEnvironment.IsAvailable) throw new InvalidOperationException("Local E2E environment missing. Prepare it explicitly; capture never resets data.");
        return result;
    }
}
