namespace LoadStock.Core;

/// <summary>
/// Uygulama verisi yolları. Tek dosya (single-file) yayınında güvenli olması için
/// LocalApplicationData altında sabit bir klasör kullanır.
/// </summary>
public static class AppPaths
{
    public static string DataDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LoadStock");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DatabasePath => Path.Combine(DataDir, "data.db");

    public static string WebViewUserDataDir
    {
        get
        {
            var dir = Path.Combine(DataDir, "WebView2");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
