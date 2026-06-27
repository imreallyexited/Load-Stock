using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using LoadStock.Core.Model;

namespace LoadStock.Core.Brands;

/// <summary>
/// Yapıştırılan ürün adreslerinden marka ve ürün kimliğini çıkarır. Asla istisna atmaz.
/// Marka, host'tan belirlenir; ardından markaya özgü kalıp uygulanır
/// (Bershka ve Stradivarius kalıpları çakışabildiği için host kontrolü şarttır).
/// </summary>
public static class UrlParsing
{
    private static readonly Regex ZaraSeoId = new(@"-p(\d+)\.html", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ZaraV1 = new(@"[?&]v1=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BershkaId = new(@"c0?p(\d+)\.html", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StradPelement = new(@"[?&]pelement=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StradId = new(@"c(\d+)p(\d+)\.html", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ColorId = new(@"[?&]colorId=(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParseAny(string url, [NotNullWhen(true)] out ProductRef? productRef)
    {
        productRef = null;
        if (!TryNormalize(url, out var normalized, out var host))
            return false;

        if (host.Contains("zara.com", StringComparison.Ordinal))
            return TryParseZara(normalized, out productRef);
        if (host.Contains("bershka.com", StringComparison.Ordinal))
            return TryParseBershka(normalized, out productRef);
        if (host.Contains("stradivarius.com", StringComparison.Ordinal))
            return TryParseStradivarius(normalized, out productRef);

        return false;
    }

    public static bool TryParseZara(string url, [NotNullWhen(true)] out ProductRef? productRef)
    {
        productRef = null;
        if (!TryNormalize(url, out var normalized, out var host) || !host.Contains("zara.com", StringComparison.Ordinal))
            return false;

        var v1 = ZaraV1.Match(normalized);
        if (v1.Success)
        {
            productRef = new ProductRef(Brand.Zara, v1.Groups[1].Value, normalized, NeedsCatalogResolve: false);
            return true;
        }

        var seo = ZaraSeoId.Match(normalized);
        if (seo.Success)
        {
            // v1 yok: gerçek katalog kimliği sayfadan çözülecek.
            productRef = new ProductRef(Brand.Zara, seo.Groups[1].Value, normalized, NeedsCatalogResolve: true);
            return true;
        }

        return false;
    }

    public static bool TryParseBershka(string url, [NotNullWhen(true)] out ProductRef? productRef)
    {
        productRef = null;
        if (!TryNormalize(url, out var normalized, out var host) || !host.Contains("bershka.com", StringComparison.Ordinal))
            return false;

        var m = BershkaId.Match(normalized);
        if (!m.Success)
            return false;

        var color = ColorId.Match(normalized);
        productRef = new ProductRef(
            Brand.Bershka,
            m.Groups[1].Value,
            normalized,
            ColorId: color.Success ? color.Groups[1].Value : null);
        return true;
    }

    public static bool TryParseStradivarius(string url, [NotNullWhen(true)] out ProductRef? productRef)
    {
        productRef = null;
        if (!TryNormalize(url, out var normalized, out var host) || !host.Contains("stradivarius.com", StringComparison.Ordinal))
            return false;

        // Yeni format: ürün kimliği "pelement" sorgu parametresinde.
        // Eski format (yedek): c{kategori}p{ürün}.html -> 2. grup.
        string? productId = null;
        var pe = StradPelement.Match(normalized);
        if (pe.Success)
        {
            productId = pe.Groups[1].Value;
        }
        else
        {
            var m = StradId.Match(normalized);
            if (m.Success)
                productId = m.Groups[2].Value;
        }

        if (productId is null)
            return false;

        var color = ColorId.Match(normalized);
        productRef = new ProductRef(
            Brand.Stradivarius,
            productId,
            normalized,
            ColorId: color.Success ? color.Groups[1].Value : null);
        return true;
    }

    private static bool TryNormalize(string? url, out string normalized, out string host)
    {
        normalized = string.Empty;
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var candidate = url.Trim();
        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return false;

        normalized = candidate;
        host = uri.Host.ToLowerInvariant();
        return true;
    }
}
