using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Toolkit.Uwp.Notifications;
using LoadStock.App.Services;
using LoadStock.App.Tray;
using LoadStock.App.ViewModels;
using LoadStock.App.Views;
using LoadStock.Core.Brands;
using LoadStock.Core.Data;
using LoadStock.Core.Fetch;
using LoadStock.WebView;

namespace LoadStock.App;

public partial class App : Application
{
    private const string InstanceMutexName = @"Local\LoadStock.SingleInstance";
    private const string ShowEventName = @"Local\LoadStock.ShowWindow";

    private Mutex? _instanceMutex;
    private IHost? _host;
    private TrayIconHost? _tray;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Geliştirme/doğrulama modu: canlı endpoint tanılaması çalıştır, dosyaya yaz, çık.
        if (e.Args.Any(a => string.Equals(a, "--diag", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunDiagnosticsAsync(e.Args);
            return;
        }

        if (e.Args.Any(a => string.Equals(a, "--dump", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunDumpAsync(e.Args);
            return;
        }

        if (e.Args.Any(a => string.Equals(a, "--verify", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunVerifyAsync(e.Args);
            return;
        }

        if (e.Args.Any(a => string.Equals(a, "--toast-test", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ToastNotifier.Setup();
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;
            var notifier = new ToastNotifier(new StockStore(LoadStock.Core.AppPaths.DatabasePath));
            var product = new TrackedProduct
            {
                Brand = LoadStock.Core.Model.Brand.Bershka,
                Name = "Test bomber ceket",
                SeoUrl = "https://www.bershka.com/tr/kemerli-suni-deri-bomber-ceket-c0p209128229.html?colorId=717",
            };
            notifier.NotifyRestock(new RestockNotification(product, new[] { new RestockedSize("209098088", "L", 119000) }));
            return; // mesaj döngüsünde kal (toast tıklaması işlensin); dışarıdan kapatılır
        }

        // Toast altyapısı: marka/ikon kaydı ve tıklama (aktivasyon) dinleyicisi.
        ToastNotifier.Setup();
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out bool isNew);
        if (!isNew)
        {
            // Zaten çalışan bir örnek var: ona pencereyi göster sinyali yolla ve çık.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { /* yarış: yoksay */ }
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // --tray ile veya bir toast tıklamasıyla açıldıysa pencereyi gösterme (tepside başla).
        bool startHidden = e.Args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase))
            || ToastNotificationManagerCompat.WasCurrentProcessToastActivated();

        _host = BuildHost();
        _host.Start();

        _tray = _host.Services.GetRequiredService<TrayIconHost>();
        _tray.Initialize();

        // İlk çalıştırmada kullanım koşulları onayı (kabul edilmezse çık).
        var store = _host.Services.GetRequiredService<StockStore>();
        if (!EnsureDisclaimerAccepted(store))
        {
            Shutdown();
            return;
        }

        // Otomatik başlatma: ayar açıksa Run anahtarını güncel exe yoluna göre tazele; değişimi dinle.
        AutostartManager.SyncFromSetting(store);
        _host.Services.GetRequiredService<MainViewModel>().Settings.AutostartChangeRequested += AutostartManager.Set;

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(ShowSignalLoop) { IsBackground = true, Name = "show-signal" };
        listener.Start();

        if (!startHidden)
            _tray.ShowMainWindow();
    }

    private IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();
        var dispatcher = Dispatcher;

        builder.Services.AddSingleton(new StockStore(LoadStock.Core.AppPaths.DatabasePath));
        builder.Services.AddSingleton<IWebFetcher>(_ => new WebViewFetcher(dispatcher, LoadStock.Core.AppPaths.WebViewUserDataDir));

        builder.Services.AddSingleton<IBrandClient>(sp => new ZaraClient(sp.GetRequiredService<IWebFetcher>()));
        builder.Services.AddSingleton<IBrandClient>(sp => new BershkaClient(sp.GetRequiredService<IWebFetcher>()));
        builder.Services.AddSingleton<IBrandClient>(sp => new StradivariusClient(sp.GetRequiredService<IWebFetcher>()));
        builder.Services.AddSingleton(sp => new BrandClientResolver(sp.GetServices<IBrandClient>()));

        builder.Services.AddSingleton<INotifier, ToastNotifier>();
        builder.Services.AddSingleton<ProductPoller>();
        builder.Services.AddSingleton<PollingService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<PollingService>());

        builder.Services.AddSingleton<ViewModels.MainViewModel>();
        builder.Services.AddSingleton<TrayIconHost>();
        builder.Services.AddSingleton<MainWindow>();
        return builder.Build();
    }

    private void ShowSignalLoop()
    {
        while (_showEvent is not null)
        {
            try { _showEvent.WaitOne(); }
            catch { break; }
            Dispatcher.Invoke(() => _tray?.ShowMainWindow());
        }
    }

    private const string DisclaimerVersion = "1";

    private static bool EnsureDisclaimerAccepted(StockStore store)
    {
        if (store.GetSetting("disclaimer_version_accepted") == DisclaimerVersion)
            return true;
        var dlg = new DisclaimerWindow();
        var accepted = dlg.ShowDialog() == true;
        if (accepted)
            store.SetSetting("disclaimer_version_accepted", DisclaimerVersion);
        return accepted;
    }

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        // Arka plan iş parçacığında çalışır. Bildirimdeki ürün adresini varsayılan tarayıcıda aç.
        try
        {
            var args = ToastArguments.Parse(e.Argument);
            if (args.TryGetValue("url", out var url) && !string.IsNullOrWhiteSpace(url))
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* aktivasyon hatası bildirimi sessizce geç */ }
    }

    private async Task RunDiagnosticsAsync(string[] args)
    {
        var outFile = GetArgValue(args, "--out")
            ?? Path.Combine(LoadStock.Core.AppPaths.DataDir, "diag.txt");
        var urls = args.Where(a => a.StartsWith("http", StringComparison.OrdinalIgnoreCase)).ToArray();

        string report;
        try
        {
            report = await LoadStock.WebView.WebViewDiagnostics.RunAsync(
                urls, LoadStock.Core.AppPaths.WebViewUserDataDir, CancellationToken.None);
        }
        catch (Exception ex)
        {
            report = "DIAG EXCEPTION:\n" + ex;
        }

        try { File.WriteAllText(outFile, report); } catch { }
        Shutdown();
    }

    private async Task RunDumpAsync(string[] args)
    {
        var outDir = GetArgValue(args, "--outdir")
            ?? Path.Combine(LoadStock.Core.AppPaths.DataDir, "dump");
        try
        {
            await LoadStock.WebView.WebViewDiagnostics.DumpAsync(
                LoadStock.Core.AppPaths.WebViewUserDataDir, outDir, CancellationToken.None);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(outDir, "_dump_exception.txt"), ex.ToString()); } catch { }
        }
        Shutdown();
    }

    private async Task RunVerifyAsync(string[] args)
    {
        var outFile = GetArgValue(args, "--out")
            ?? Path.Combine(LoadStock.Core.AppPaths.DataDir, "verify.txt");
        var urls = args.Where(a => a.StartsWith("http", StringComparison.OrdinalIgnoreCase)).ToArray();

        var sb = new System.Text.StringBuilder();
        var fetcher = new LoadStock.WebView.WebViewFetcher(Dispatcher, LoadStock.Core.AppPaths.WebViewUserDataDir);
        LoadStock.Core.Brands.IBrandClient[] clients =
        {
            new LoadStock.Core.Brands.ZaraClient(fetcher),
            new LoadStock.Core.Brands.BershkaClient(fetcher),
            new LoadStock.Core.Brands.StradivariusClient(fetcher),
        };

        try
        {
            foreach (var url in urls)
            {
                sb.AppendLine("============================================================");
                sb.AppendLine("URL: " + url);

                var client = clients.FirstOrDefault(c => c.TryParseUrl(url, out _));
                if (client is null)
                {
                    sb.AppendLine("  -> desteklenmeyen URL");
                    continue;
                }
                client.TryParseUrl(url, out var pref);
                sb.AppendLine($"  marka={client.Brand} productId={pref!.ProductId} colorId={pref.ColorId ?? "-"}");

                try
                {
                    var info = await client.FetchInfoAsync(pref, CancellationToken.None);
                    var avail = await client.FetchAvailabilityAsync(pref, CancellationToken.None);
                    var labels = info.Sizes.ToDictionary(s => s.SizeId, s => s);

                    sb.AppendLine($"  ürün: {info.Name}");
                    sb.AppendLine($"  beden sayısı: info={info.Sizes.Count} availability={avail.Count}");
                    var watched = info.Sizes.Select(s => s.SizeId).ToHashSet();
                    var inStock = 0;
                    foreach (var a in avail)
                    {
                        // İzlenen renge ait bedenleri işaretle (info'daki sku kümesi).
                        var known = labels.TryGetValue(a.SizeId, out var si);
                        if (!known && watched.Count > 0) continue; // bu renge ait değil
                        if (a.InStock) inStock++;
                        var label = known ? si!.Label : a.SizeLabel;
                        var price = (known ? si!.PriceMinor : a.PriceMinor) is long m ? $"{m / 100.0:0.00}" : "?";
                        sb.AppendLine($"     {(a.InStock ? "[STOKTA]" : "[ tükendi ]")} {a.SizeId} {label,-6} {price,8} ({a.RawState})");
                    }
                    sb.AppendLine($"  >>> izlenen renkte stokta olan beden sayısı: {inStock}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  [HATA] {ex.GetType().Name}: {ex.Message}");
                }
                sb.AppendLine();
            }
        }
        finally
        {
            fetcher.Dispose();
        }

        try { File.WriteAllText(outFile, sb.ToString()); } catch { }
        Shutdown();
    }

    private static string? GetArgValue(string[] args, string name)
    {
        foreach (var a in args)
            if (a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return a.Substring(name.Length + 1);
        return null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            _host.Dispose();
        }
        if (_instanceMutex is not null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch { }
            _instanceMutex.Dispose();
        }
        base.OnExit(e);
    }
}
