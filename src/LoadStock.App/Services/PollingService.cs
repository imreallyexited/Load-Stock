using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using LoadStock.Core.Data;
using LoadStock.Core.Polling;

namespace LoadStock.App.Services;

/// <summary>
/// Arka plan yoklama servisi. 15 sn'de bir uyanır, vadesi gelmiş tek bir ürünü kontrol eder
/// (sıralı çalıştığı için doğal olarak tek-akışlı). Başarıda interval±jitter, 403/hatada üssel
/// backoff uygular. Pencere kapalıyken de çalışır; gerçek poll mantığı ProductPoller'dadır.
/// </summary>
public sealed class PollingService : IHostedService
{
    private const int TickSeconds = 15;
    private const int MinIntervalSeconds = 180;
    private const int MaxBackoffSeconds = 3600;
    private const int DefaultIntervalSeconds = 300;
    private const int DefaultJitterSeconds = 90;

    private readonly StockStore _store;
    private readonly ProductPoller _poller;
    private readonly Dictionary<long, RuntimeState> _runtime = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public PollingService(StockStore store, ProductPoller poller)
    {
        _store = store;
        _poller = poller;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
            catch { /* zaman aşımı/iptal: yoksay */ }
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(TickSeconds));
        try
        {
            do
            {
                try { await TickAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Trace.WriteLine($"[poll] tick hatası: {ex.Message}"); }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { /* normal kapanış */ }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.Now;
        var products = _store.GetProducts().Where(p => !p.Paused).ToList();

        var ids = products.Select(p => p.Id).ToHashSet();
        foreach (var gone in _runtime.Keys.Where(k => !ids.Contains(k)).ToList())
            _runtime.Remove(gone);
        foreach (var p in products)
            if (!_runtime.ContainsKey(p.Id))
                _runtime[p.Id] = new RuntimeState { DueAt = now };

        var dueProduct = products
            .Where(p => _runtime[p.Id].DueAt <= now)
            .OrderBy(p => _runtime[p.Id].DueAt)
            .FirstOrDefault();
        if (dueProduct is null)
            return;

        var rt = _runtime[dueProduct.Id];
        var interval = Math.Max(MinIntervalSeconds, ReadInt("poll_interval_sec", DefaultIntervalSeconds));
        var jitter = Math.Max(0, ReadInt("jitter_sec", DefaultJitterSeconds));

        try
        {
            await _poller.PollAsync(dueProduct, ct);
            rt.FailCount = 0;
            rt.DueAt = DateTimeOffset.Now.AddSeconds(WithJitter(interval, jitter));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            rt.FailCount++;
            var backoff = Backoff.NextDelaySeconds(interval, rt.FailCount, MaxBackoffSeconds);
            rt.DueAt = DateTimeOffset.Now.AddSeconds(WithJitter(backoff, jitter));
            Trace.WriteLine($"[poll] {dueProduct.Brand} {dueProduct.ProductId} hata (#{rt.FailCount}): {ex.Message}");
        }
    }

    private int ReadInt(string key, int fallback)
        => int.TryParse(_store.GetSetting(key), out var v) ? v : fallback;

    private static int WithJitter(int baseSeconds, int jitterSeconds)
    {
        if (jitterSeconds <= 0) return baseSeconds;
        var delta = Random.Shared.Next(-jitterSeconds, jitterSeconds + 1);
        return Math.Max(30, baseSeconds + delta);
    }

    private sealed class RuntimeState
    {
        public DateTimeOffset DueAt { get; set; }
        public int FailCount { get; set; }
    }
}
