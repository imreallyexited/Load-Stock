using CommunityToolkit.Mvvm.ComponentModel;
using LoadStock.Core.Data;

namespace LoadStock.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly StockStore _store;
    private bool _loading;

    [ObservableProperty] private int _intervalMinutes = 5;
    [ObservableProperty] private int _jitterSeconds = 90;
    [ObservableProperty] private bool _soundEnabled = true;
    [ObservableProperty] private bool _autostart;
    [ObservableProperty] private string _webViewStatus = string.Empty;

    public SettingsViewModel(StockStore store)
    {
        _store = store;
        Load();
    }

    private void Load()
    {
        _loading = true;
        try
        {
            if (int.TryParse(_store.GetSetting("poll_interval_sec"), out var sec))
                IntervalMinutes = Math.Max(3, sec / 60);
            if (int.TryParse(_store.GetSetting("jitter_sec"), out var j))
                JitterSeconds = Math.Max(0, j);
            SoundEnabled = _store.GetSetting("sound_enabled") != "0";
            Autostart = _store.GetSetting("autostart") == "1";

            var version = LoadStock.WebView.WebView2Bootstrap.GetInstalledVersion();
            WebViewStatus = version is null ? "Edge WebView2 çalışma zamanı BULUNAMADI" : $"Edge WebView2 {version}";
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnIntervalMinutesChanged(int value)
    {
        if (_loading) return;
        var minutes = Math.Max(3, value);
        _store.SetSetting("poll_interval_sec", (minutes * 60).ToString());
    }

    partial void OnJitterSecondsChanged(int value)
    {
        if (_loading) return;
        _store.SetSetting("jitter_sec", Math.Max(0, value).ToString());
    }

    partial void OnSoundEnabledChanged(bool value)
    {
        if (_loading) return;
        _store.SetSetting("sound_enabled", value ? "1" : "0");
    }

    // Autostart kaydı M7'de (AutostartManager) bağlanır.
    public event Action<bool>? AutostartChangeRequested;

    partial void OnAutostartChanged(bool value)
    {
        if (_loading) return;
        _store.SetSetting("autostart", value ? "1" : "0");
        AutostartChangeRequested?.Invoke(value);
    }
}
