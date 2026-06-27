using System.Collections.Concurrent;
using System.IO;
using System.Windows.Threading;
using LoadStock.Core.Brands;
using LoadStock.Core.Fetch;

namespace LoadStock.WebView;

/// <summary>
/// <see cref="IWebFetcher"/> gerçekleştirimi. Her marka kökeni için ayrı bir ısıtılmış WebView2
/// bağlamı tutar (Akamai çerez kavanozları origin'e özgüdür). Tüm WebView2 erişimi tek bir
/// Dispatcher (STA) iş parçacığına marshal edilir. 403 alındığında bağlamı yeniden ısıtıp bir kez tekrar dener.
/// </summary>
public sealed class WebViewFetcher : IWebFetcher, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly string _baseUserDataDir;
    private readonly ConcurrentDictionary<string, WarmWebView> _contexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initGate = new(1, 1);

    public WebViewFetcher(Dispatcher dispatcher, string baseUserDataDir)
    {
        _dispatcher = dispatcher;
        _baseUserDataDir = baseUserDataDir;
    }

    public async Task<FetchResult> FetchJsonAsync(string origin, string url, CancellationToken ct)
    {
        var op = _dispatcher.InvokeAsync(async () =>
        {
            var ctx = await GetOrCreateContextAsync(origin, ct);
            var result = await ctx.FetchJsonAsync(url, ct);

            if (result.IsBlocked)
            {
                // Bağlamı yeniden ısıt (sensor.js + _abck tazele) ve bir kez tekrar dene.
                await ReWarmAsync(origin, ct);
                ctx = await GetOrCreateContextAsync(origin, ct);
                result = await ctx.FetchJsonAsync(url, ct);
            }

            return result;
        });

        return await op.Task.Unwrap();
    }

    private async Task<WarmWebView> GetOrCreateContextAsync(string origin, CancellationToken ct)
    {
        if (_contexts.TryGetValue(origin, out var existing))
            return existing;

        await _initGate.WaitAsync(ct);
        try
        {
            if (_contexts.TryGetValue(origin, out existing))
                return existing;

            var ctx = new WarmWebView(UserDataDirFor(origin));
            await ctx.EnsureReadyAsync(WarmNavFor(origin), ct);
            _contexts[origin] = ctx;
            return ctx;
        }
        finally
        {
            _initGate.Release();
        }
    }

    private async Task ReWarmAsync(string origin, CancellationToken ct)
    {
        if (_contexts.TryRemove(origin, out var old))
            old.Dispose();
        await GetOrCreateContextAsync(origin, ct);
    }

    private static string WarmNavFor(string origin)
    {
        foreach (var cfg in BrandCatalog.All)
            if (string.Equals(cfg.Origin, origin, StringComparison.OrdinalIgnoreCase))
                return cfg.WarmNavUrl;
        // Bilinmeyen origin: kökün kendisini ısıt.
        return origin.TrimEnd('/') + "/";
    }

    private string UserDataDirFor(string origin)
    {
        var host = new Uri(origin).Host.Replace('.', '_');
        var dir = Path.Combine(_baseUserDataDir, host);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var ctx in _contexts.Values)
            ctx.Dispose();
        _contexts.Clear();
        _initGate.Dispose();
    }
}
