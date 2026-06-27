using Microsoft.Win32;
using LoadStock.Core.Data;

namespace LoadStock.App.Services;

/// <summary>
/// Windows oturum açılışında otomatik başlatmayı HKCU Run anahtarıyla yönetir.
/// Tek dosya yayında boş dönen Assembly.Location yerine Environment.ProcessPath kullanılır.
/// </summary>
public static class AutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LoadStock";

    public static void Set(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }

    public static void Enable()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key?.SetValue(ValueName, $"\"{exe}\" --tray", RegistryValueKind.String);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is not null;
    }

    /// <summary>Ayar açıksa Run anahtarını güncel exe yoluna göre tazeler (uygulama taşınmışsa düzelir).</summary>
    public static void SyncFromSetting(StockStore store)
    {
        try
        {
            if (store.GetSetting("autostart") == "1")
                Enable();
        }
        catch { /* kayıt defteri hatası kritik değil */ }
    }
}
