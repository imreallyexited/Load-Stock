using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using LoadStock.Core.Data;

namespace LoadStock.App.Services;

/// <summary>
/// Windows Action Center toast bildirimleri. Bildirime tıklanınca ürün sayfası varsayılan
/// tarayıcıda açılır (argümanlar OnActivated'da çözülür). Ses ayardan kapatılabilir.
/// </summary>
public sealed class ToastNotifier : INotifier
{
    public const string Aumid = "LoadStock.Inditex.Watcher";

    private readonly StockStore _store;

    public ToastNotifier(StockStore store) => _store = store;

    public void NotifyRestock(RestockNotification n)
    {
        var soundEnabled = _store.GetSetting("sound_enabled") != "0"; // varsayılan açık
        var brand = n.Product.Brand.ToString();
        var title = string.IsNullOrWhiteSpace(n.Product.Name)
            ? $"{brand} — stoğa girdi"
            : $"{n.Product.Name} — stoğa girdi";
        var body = $"{brand} · {string.Join(", ", n.Sizes.Select(FormatSize))}";

        var builder = new ToastContentBuilder()
            .AddArgument("action", "open")
            .AddArgument("url", n.Product.SeoUrl)
            .AddText(title)
            .AddText(body);

        builder.AddAudio(new Uri("ms-winsoundevent:Notification.Default"), loop: false, silent: !soundEnabled);
        builder.Show();
    }

    private static string FormatSize(RestockedSize s)
    {
        var label = string.IsNullOrEmpty(s.Label) ? s.SizeId : s.Label;
        return s.PriceMinor is long m ? $"{label} ({m / 100.0:0.##}₺)" : label;
    }

    // ---- kurulum (AUMID + marka adı/ikonu) ----

    /// <summary>Süreç AUMID'ini ayarlar ve toast'ta görünecek ad/ikonu HKCU'ya yazar.</summary>
    public static void Setup()
    {
        try { SetCurrentProcessExplicitAppUserModelID(Aumid); } catch { }

        try
        {
            var iconPath = ExtractIconToDisk();
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{Aumid}");
            key?.SetValue("DisplayName", "LoadStock", RegistryValueKind.String);
            if (iconPath is not null && File.Exists(iconPath))
                key?.SetValue("IconUri", iconPath, RegistryValueKind.String);
        }
        catch { /* branding kritik değil */ }
    }

    private static string? ExtractIconToDisk()
    {
        try
        {
            var target = Path.Combine(LoadStock.Core.AppPaths.DataDir, "toast-logo.png");
            var res = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/toast-logo.png", UriKind.Absolute));
            if (res is null) return File.Exists(target) ? target : null;
            using var src = res.Stream;
            using var dst = File.Create(target);
            src.CopyTo(dst);
            return target;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appID);
}
