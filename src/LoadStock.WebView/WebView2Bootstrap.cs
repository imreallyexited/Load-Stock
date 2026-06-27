using Microsoft.Web.WebView2.Core;

namespace LoadStock.WebView;

/// <summary>Edge WebView2 Evergreen runtime'ının varlığını denetler.</summary>
public static class WebView2Bootstrap
{
    public static string? GetInstalledVersion()
    {
        try
        {
            var v = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return string.IsNullOrEmpty(v) ? null : v;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsRuntimeAvailable() => GetInstalledVersion() is not null;
}
