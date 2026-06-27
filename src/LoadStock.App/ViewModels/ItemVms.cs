using CommunityToolkit.Mvvm.ComponentModel;

namespace LoadStock.App.ViewModels;

/// <summary>Takip listesindeki bir ürünün satırı.</summary>
public partial class TrackedItemVm : ObservableObject
{
    public long Id { get; init; }
    public string SeoUrl { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _color = string.Empty;
    [ObservableProperty] private string _watch = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _lastChecked = string.Empty;
    [ObservableProperty] private bool _paused;

    public string PauseLabel => Paused ? "Sürdür" : "Duraklat";

    partial void OnPausedChanged(bool value) => OnPropertyChanged(nameof(PauseLabel));
}

/// <summary>Geçmiş listesindeki bir stoğa-giriş olayı.</summary>
public sealed class HistoryItemVm
{
    public string When { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Product { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Price { get; init; } = string.Empty;
    public string SeoUrl { get; init; } = string.Empty;
}
