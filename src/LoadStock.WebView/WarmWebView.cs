using System.Collections.Concurrent;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using LoadStock.Core.Fetch;

namespace LoadStock.WebView;

/// <summary>
/// Tek bir marka kökeni için ısıtılmış, ekran dışında render edilen bir WebView2 bağlamı.
/// İstekler sayfa içinden (fetch + postMessage) yapılır; böylece gerçek Chromium TLS parmak izi
/// ve doğrulanmış Akamai çerezleri miras alınır. Bu sınıf bir Dispatcher (STA) iş parçacığında
/// oluşturulup kullanılmalıdır.
/// </summary>
public sealed class WarmWebView : IDisposable
{
    private readonly string _userDataDir;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<FetchResult>> _pending = new();
    private readonly ConcurrentBag<string> _capturedRequests = new();
    private readonly ConcurrentBag<string> _capturedUrls = new();

    private Window? _window;
    private WebView2? _wv;
    private long _reqCounter;
    private bool _ready;
    private bool _captureEnabled;

    public WarmWebView(string userDataDir)
    {
        _userDataDir = userDataDir;
    }

    /// <summary>Tanılama için gözlemlenen itxrest / products-details istekleri (durum + adres).</summary>
    public IReadOnlyCollection<string> CapturedRequests => _capturedRequests.ToArray();

    /// <summary>Gözlemlenen ilgili isteklerin ham adresleri (yeniden fetch için).</summary>
    public IReadOnlyCollection<string> CapturedUrls => _capturedUrls.ToArray();

    public void EnableRequestCapture() => _captureEnabled = true;

    public async Task EnsureReadyAsync(string warmNavUrl, CancellationToken ct)
    {
        if (_ready) return;

        _window = new Window
        {
            Width = 1280,
            Height = 900,
            Left = -32000,
            Top = -32000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Title = "sl-bg",
        };
        _wv = new WebView2();
        _window.Content = _wv;
        _window.Show(); // ekran dışında ama gerçekten render edilir (Akamai sensör sinyali için)

        var options = new CoreWebView2EnvironmentOptions();
        var env = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: _userDataDir, options: options);
        await _wv.EnsureCoreWebView2Async(env);

        var core = _wv.CoreWebView2;
        core.WebMessageReceived += OnWebMessageReceived;
        core.WebResourceResponseReceived += OnWebResourceResponseReceived;

        await NavigateAsync(warmNavUrl, ct);
        await WaitForAbckAsync(warmNavUrl, ct);
        _ready = true;
    }

    public Task NavigateAsync(string url, CancellationToken ct)
    {
        var core = _wv!.CoreWebView2;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            core.NavigationCompleted -= Handler;
            tcs.TrySetResult(e.IsSuccess);
        }

        core.NavigationCompleted += Handler;
        core.Navigate(url);
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(45), ct);
    }

    public async Task<string> ExecuteScriptAsync(string javascript)
        => await _wv!.CoreWebView2.ExecuteScriptAsync(javascript);

    /// <summary>Sayfa içinden (origin ile aynı kökende) bir JSON adresini getirir.</summary>
    public async Task<FetchResult> FetchJsonAsync(string url, CancellationToken ct)
    {
        var id = System.Threading.Interlocked.Increment(ref _reqCounter).ToString();
        var tcs = new TaskCompletionSource<FetchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        // JS şablonunda küme ayracı kaçışı sorunlarından kaçınmak için yer tutucular kullanılır.
        // Esc(...) JSON ile tırnaklanmış güvenli bir JS dizesi üretir.
        string Esc(string s) => JsonSerializer.Serialize(s);
        const string template = """
        (function(){
          fetch(__URL__, {credentials:'include', headers:{'Accept':'application/json, text/plain, */*'}})
            .then(function(r){ return r.text().then(function(b){ return {status:r.status, body:b}; }); })
            .then(function(o){ window.chrome.webview.postMessage(JSON.stringify({id:__ID__, ok:true, status:o.status, body:o.body})); })
            .catch(function(e){ window.chrome.webview.postMessage(JSON.stringify({id:__ID__, ok:false, error:String(e)})); });
        })();
        """;
        var js = template.Replace("__URL__", Esc(url)).Replace("__ID__", Esc(id));

        try
        {
            await _wv!.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch
        {
            _pending.TryRemove(id, out _);
            throw;
        }

        using (ct.Register(() => { if (_pending.TryRemove(id, out var p)) p.TrySetCanceled(); }))
        {
            var done = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30), ct));
            if (done != tcs.Task)
            {
                _pending.TryRemove(id, out _);
                throw new TimeoutException($"Sayfa içi fetch zaman aşımına uğradı: {url}");
            }
            return await tcs.Task;
        }
    }

    public async Task<string?> GetAbckStateAsync(string url)
    {
        var cookies = await _wv!.CoreWebView2.CookieManager.GetCookiesAsync(url);
        var abck = cookies.FirstOrDefault(c => c.Name == "_abck");
        return abck?.Value;
    }

    private async Task WaitForAbckAsync(string url, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var value = await GetAbckStateAsync(url);
            // Doğrulanmamış _abck "~-1~" içerir; sensor.js doğruladıktan sonra bu kalkar.
            if (!string.IsNullOrEmpty(value) && !value.Contains("~-1~"))
                return;
            await Task.Delay(700, ct);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw;
        try { raw = e.TryGetWebMessageAsString(); }
        catch { return; }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var idEl))
                return;
            var id = idEl.GetString();
            if (id is null || !_pending.TryRemove(id, out var tcs))
                return;

            if (root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean())
            {
                var status = root.GetProperty("status").GetInt32();
                var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? string.Empty : string.Empty;
                tcs.TrySetResult(new FetchResult(status, body));
            }
            else
            {
                var err = root.TryGetProperty("error", out var er) ? er.GetString() : "fetch başarısız";
                tcs.TrySetException(new InvalidOperationException(err));
            }
        }
        catch
        {
            // postMessage bizim formatımızda değil: yoksay.
        }
    }

    private void OnWebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (!_captureEnabled) return;
        try
        {
            var uri = e.Request.Uri;
            if (uri.Contains("/itxrest/", StringComparison.OrdinalIgnoreCase)
                || uri.Contains("products-details", StringComparison.OrdinalIgnoreCase)
                || uri.Contains("/stock", StringComparison.OrdinalIgnoreCase)
                || uri.Contains("/availability", StringComparison.OrdinalIgnoreCase)
                || uri.Contains("seo/config", StringComparison.OrdinalIgnoreCase))
            {
                _capturedRequests.Add($"{e.Response.StatusCode} {e.Request.Method} {uri}");
                _capturedUrls.Add(uri);
            }
        }
        catch
        {
            // yoksay
        }
    }

    public void Dispose()
    {
        try
        {
            if (_wv?.CoreWebView2 is { } core)
            {
                core.WebMessageReceived -= OnWebMessageReceived;
                core.WebResourceResponseReceived -= OnWebResourceResponseReceived;
            }
        }
        catch { }

        try { _wv?.Dispose(); } catch { }
        try { _window?.Close(); } catch { }
        _wv = null;
        _window = null;
        _ready = false;
    }
}
