using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoadStock.App.Services;
using LoadStock.Core.Data;

namespace LoadStock.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly StockStore _store;
    private readonly ProductPoller _poller;
    private readonly BrandClientResolver _resolver;

    public MainViewModel(StockStore store, ProductPoller poller, BrandClientResolver resolver)
    {
        _store = store;
        _poller = poller;
        _resolver = resolver;
        Settings = new SettingsViewModel(store);
        Load();
    }

    public ObservableCollection<TrackedItemVm> Tracked { get; } = new();
    public ObservableCollection<HistoryItemVm> History { get; } = new();
    public SettingsViewModel Settings { get; }
    public BrandClientResolver Resolver => _resolver;
    public StockStore Store => _store;

    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _statusText = string.Empty;

    public bool NotBusy => !Busy;
    partial void OnBusyChanged(bool value) => OnPropertyChanged(nameof(NotBusy));

    public void Load()
    {
        Tracked.Clear();
        foreach (var p in _store.GetProducts())
            Tracked.Add(BuildItem(p));

        History.Clear();
        foreach (var e in _store.GetRecentEvents())
        {
            History.Add(new HistoryItemVm
            {
                When = e.OccurredAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm"),
                Brand = e.Brand.ToString(),
                Product = e.ProductName ?? string.Empty,
                Size = e.SizeLabel ?? e.SizeId,
                Price = e.PriceMinor is long m ? $"{m / 100.0:0.##}₺" : string.Empty,
                SeoUrl = e.SeoUrl,
            });
        }

        StatusText = Tracked.Count == 0
            ? "Henüz ürün eklenmedi. Bir ürün linki yapıştırarak başlayın."
            : $"{Tracked.Count} ürün izleniyor.";
    }

    private TrackedItemVm BuildItem(TrackedProduct p)
    {
        var states = _store.GetLastStates(p.Id);
        var sizeInfos = _store.GetSizeInfos(p.Id);
        var watchedIds = p.WatchAnySize
            ? states.Keys.ToHashSet()
            : _store.GetWatchedSizes(p.Id).ToHashSet();

        string status;
        string lastChecked = "—";
        if (states.Count == 0)
        {
            status = "henüz kontrol edilmedi";
        }
        else
        {
            var relevant = watchedIds.Count > 0 ? watchedIds : states.Keys.ToHashSet();
            var inStock = relevant.Count(id => states.TryGetValue(id, out var s) && s.InStock);
            status = inStock > 0 ? $"{inStock} bedende STOK VAR" : "stokta beden yok";
            lastChecked = states.Values.Max(s => s.CheckedAt).LocalDateTime.ToString("HH:mm");
        }

        var colorName = sizeInfos.FirstOrDefault(s =>
            !string.IsNullOrEmpty(p.ColorId) && string.Equals(s.ColorId, p.ColorId, StringComparison.OrdinalIgnoreCase))?.ColorName;

        return new TrackedItemVm
        {
            Id = p.Id,
            SeoUrl = p.SeoUrl,
            Brand = p.Brand.ToString(),
            Name = p.Name ?? p.ProductId,
            Color = string.IsNullOrEmpty(colorName) ? (string.IsNullOrEmpty(p.ColorId) ? "tüm renkler" : p.ColorId) : colorName,
            Watch = p.WatchAnySize ? "tüm bedenler" : $"{watchedIds.Count} beden",
            Status = status,
            LastChecked = lastChecked,
            Paused = p.Paused,
        };
    }

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        if (Busy) return;
        Busy = true;
        try
        {
            var products = _store.GetProducts().Where(p => !p.Paused).ToList();
            var done = 0;
            foreach (var p in products)
            {
                StatusText = $"Kontrol ediliyor ({++done}/{products.Count}): {p.Name}";
                try { await _poller.PollAsync(p, CancellationToken.None); }
                catch (Exception ex) { Trace.WriteLine($"[ui-refresh] {p.ProductId}: {ex.Message}"); }
            }
            Load();
            StatusText = $"Kontrol tamamlandı ({products.Count} ürün).";
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private void Remove(TrackedItemVm? item)
    {
        if (item is null) return;
        _store.RemoveProduct(item.Id);
        Load();
    }

    [RelayCommand]
    private void TogglePause(TrackedItemVm? item)
    {
        if (item is null) return;
        _store.SetPaused(item.Id, !item.Paused);
        Load();
    }

    [RelayCommand]
    private void Open(TrackedItemVm? item)
    {
        if (item is null || string.IsNullOrEmpty(item.SeoUrl)) return;
        try { Process.Start(new ProcessStartInfo(item.SeoUrl) { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenHistory(HistoryItemVm? item)
    {
        if (item is null || string.IsNullOrEmpty(item.SeoUrl)) return;
        try { Process.Start(new ProcessStartInfo(item.SeoUrl) { UseShellExecute = true }); } catch { }
    }
}
