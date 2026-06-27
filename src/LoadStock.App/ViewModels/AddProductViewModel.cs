using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoadStock.App.Services;
using LoadStock.Core.Brands;
using LoadStock.Core.Data;
using LoadStock.Core.Model;

namespace LoadStock.App.ViewModels;

public partial class SizePickVm : ObservableObject
{
    public string SizeId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? ColorName { get; init; }
    public string PriceText { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _selected;

    public string Display => string.IsNullOrEmpty(ColorName)
        ? $"{Label}  {PriceText}"
        : $"{Label}  ({ColorName})  {PriceText}";
}

public partial class AddProductViewModel : ObservableObject
{
    private readonly StockStore _store;
    private readonly BrandClientResolver _resolver;

    private IBrandClient? _client;
    private ProductRef? _ref;

    public AddProductViewModel(StockStore store, BrandClientResolver resolver)
    {
        _store = store;
        _resolver = resolver;
    }

    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _productName;
    [ObservableProperty] private bool _loaded;
    [ObservableProperty] private bool _watchAnySize = true;

    public bool SpecificSizes => !WatchAnySize;
    partial void OnWatchAnySizeChanged(bool value) => OnPropertyChanged(nameof(SpecificSizes));

    public ObservableCollection<SizePickVm> Sizes { get; } = new();

    /// <summary>Kaydetme başarılıysa true; pencere buna göre kapanır.</summary>
    public bool Saved { get; private set; }

    [RelayCommand]
    private async Task FetchAsync()
    {
        Error = null;
        Loaded = false;
        Sizes.Clear();
        ProductName = null;

        var client = _resolver.Match(Url, out var pref);
        if (client is null || pref is null)
        {
            Error = "Bu adres tanınmadı. Zara, Bershka veya Stradivarius ürün linki yapıştırın.";
            return;
        }

        _client = client;
        _ref = pref;

        try
        {
            Busy = true;
            var info = await client.FetchInfoAsync(pref, CancellationToken.None);
            ProductName = info.Name;
            foreach (var s in info.Sizes)
            {
                Sizes.Add(new SizePickVm
                {
                    SizeId = s.SizeId,
                    Label = s.Label,
                    ColorName = s.ColorName,
                    PriceText = s.PriceMinor is long m ? $"{m / 100.0:0.##}₺" : string.Empty,
                });
            }
            Loaded = Sizes.Count > 0;
            if (Sizes.Count == 0)
                Error = "Ürün bilgisi alındı ama beden bulunamadı.";
        }
        catch (Exception ex)
        {
            Error = "Bilgi alınamadı: " + ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (_client is null || _ref is null)
            return;

        var info = Sizes.Select(s => new SizeInfo(s.SizeId, s.Label, ParsePrice(s.PriceText), null, s.ColorName)).ToList();
        var watched = WatchAnySize ? Array.Empty<string>() : Sizes.Where(s => s.Selected).Select(s => s.SizeId).ToArray();

        if (!WatchAnySize && watched.Length == 0)
        {
            Error = "En az bir beden seçin ya da 'Tüm bedenler'i işaretleyin.";
            return;
        }

        var pk = _store.AddOrUpdateProduct(_client.Brand, _ref.ProductId, _ref.ColorId, _ref.SeoUrl, ProductName, WatchAnySize);
        _store.SetSizeInfos(pk, info);
        _store.SetWatchedSizes(pk, watched);
        Saved = true;
    }

    private static long? ParsePrice(string text)
    {
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return long.TryParse(digits, out var v) ? v : null;
    }
}
