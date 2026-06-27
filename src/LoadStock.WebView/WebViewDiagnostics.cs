using System.Text;
using LoadStock.Core.Brands;
using LoadStock.Core.Model;

namespace LoadStock.WebView;

/// <summary>
/// Geliştirme/doğrulama amaçlı tanılama. Verilen ürün adresleri için marka kökenini ısıtır,
/// ürün sayfasını açıp itxrest trafiğini pasif yakalar, ardından yakalanan ürün-veri
/// adreslerini ve kurgulanan aday adresleri sayfa içinden yeniden fetch ederek JSON gövdesini döker.
/// Çıktı, canlı endpoint'leri ve JSON şekillerini doğrulamak için kullanılır.
/// </summary>
public static class WebViewDiagnostics
{
    public static async Task<string> RunAsync(IReadOnlyList<string> productUrls, string baseUserDataDir, CancellationToken ct)
    {
        var sb = new StringBuilder();
        void Log(string line) => sb.AppendLine(line);

        Log($"WebView2 runtime: {WebView2Bootstrap.GetInstalledVersion() ?? "(YOK)"}");
        Log($"zaman: {DateTimeOffset.Now:O}");
        Log("");

        foreach (var url in productUrls)
        {
            Log("============================================================");
            Log($"URL: {url}");

            var brand = BrandFromHost(url);
            if (brand is null)
            {
                Log("  -> bilinmeyen marka (host eşleşmedi).");
                Log("");
                continue;
            }

            var cfg = BrandCatalog.For(brand.Value);
            UrlParsing.TryParseAny(url, out var pref);
            Log($"  marka={brand} parsedProductId={pref?.ProductId ?? "(parse YOK)"} colorId={pref?.ColorId ?? "-"} resolveCatalog={pref?.NeedsCatalogResolve}");

            WarmWebView? ctx = null;
            try
            {
                ctx = new WarmWebView(System.IO.Path.Combine(baseUserDataDir, "diag_" + brand));
                ctx.EnableRequestCapture();

                Log($"  ısınıyor: {cfg.WarmNavUrl}");
                await ctx.EnsureReadyAsync(cfg.WarmNavUrl, ct);
                Log($"  _abck (warm): {AbckShort(await ctx.GetAbckStateAsync(cfg.WarmNavUrl))}");

                Log("  ürün sayfası açılıyor...");
                await ctx.NavigateAsync(url, ct);
                await Task.Delay(TimeSpan.FromSeconds(7), ct); // XHR'lerin oturmasını bekle
                Log($"  _abck (ürün): {AbckShort(await ctx.GetAbckStateAsync(cfg.Origin))}");

                Log("  -- yakalanan itxrest/products-details istekleri --");
                var captured = ctx.CapturedRequests.Distinct().OrderBy(x => x).ToList();
                if (captured.Count == 0) Log("     (yok)");
                foreach (var c in captured) Log("     " + c);

                // Yeniden fetch edilecek adresler: yakalanan ürün-veri adresleri + kurgulanan adaylar.
                var toFetch = new List<string>();
                foreach (var u in ctx.CapturedUrls)
                    if (LooksLikeProductData(u))
                        toFetch.Add(u);
                toFetch.AddRange(BuildCandidates(cfg, pref));
                toFetch = toFetch.Distinct().ToList();

                Log("  -- sayfa-içi fetch (yakalanan + aday) --");
                if (toFetch.Count == 0) Log("     (aday yok)");
                foreach (var f in toFetch)
                {
                    try
                    {
                        var res = await ctx.FetchJsonAsync(f, ct);
                        Log($"     [{res.Status}] {f}");
                        Log($"        body: {Trunc(res.Body, 2000)}");
                    }
                    catch (Exception ex)
                    {
                        Log($"     [HATA] {f} -> {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"  [İSTİSNA] {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                ctx?.Dispose();
            }

            Log("");
        }

        return sb.ToString();
    }

    /// <summary>Doğrulanmış endpoint'lerin TAM gövdelerini ayrı dosyalara döker (şekil incelemesi için).</summary>
    public static async Task DumpAsync(string baseUserDataDir, string outDir, CancellationToken ct)
    {
        System.IO.Directory.CreateDirectory(outDir);

        var groups = new (Brand brand, string warmNav, (string name, string url)[] eps)[]
        {
            (Brand.Bershka, "https://www.bershka.com/tr/", new[]
            {
                ("bershka_productsArray", "https://www.bershka.com/itxrest/3/catalog/store/44109521/40259537/productsArray?productIds=209128229&languageId=-43&appId=1"),
                ("bershka_stock", "https://www.bershka.com/itxrest/2/catalog/store/44109521/40259537/product/209128229/stock?languageId=-43&appId=1"),
            }),
            (Brand.Zara, "https://www.zara.com/tr/tr/", new[]
            {
                ("zara_products_details", "https://www.zara.com/tr/tr/products-details?productIds=562838470&ajax=true"),
                ("zara_availability", "https://www.zara.com/itxrest/1/catalog/store/11766/product/id/562838470/availability"),
            }),
            (Brand.Stradivarius, "https://www.stradivarius.com/tr/", new[]
            {
                ("stradivarius_detail", "https://www.stradivarius.com/itxrest/2/catalog/store/54009571/50331068/category/0/product/475933969/detail?languageId=-43&appId=1"),
            }),
        };

        foreach (var (brand, warmNav, eps) in groups)
        {
            WarmWebView? ctx = null;
            try
            {
                ctx = new WarmWebView(System.IO.Path.Combine(baseUserDataDir, "dump_" + brand));
                await ctx.EnsureReadyAsync(warmNav, ct);
                foreach (var (name, url) in eps)
                {
                    try
                    {
                        var res = await ctx.FetchJsonAsync(url, ct);
                        System.IO.File.WriteAllText(
                            System.IO.Path.Combine(outDir, $"{name}_{res.Status}.json"), res.Body);
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.WriteAllText(
                            System.IO.Path.Combine(outDir, $"{name}_ERROR.txt"), ex.ToString());
                    }
                }
            }
            finally
            {
                ctx?.Dispose();
            }
        }
    }

    private static Brand? BrandFromHost(string url)
    {
        var candidate = url.Trim();
        if (!candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            candidate = "https://" + candidate;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return null;
        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("zara.com")) return Brand.Zara;
        if (host.Contains("bershka.com")) return Brand.Bershka;
        if (host.Contains("stradivarius.com")) return Brand.Stradivarius;
        return null;
    }

    private static bool LooksLikeProductData(string url)
    {
        return url.Contains("products-details", StringComparison.OrdinalIgnoreCase)
            || url.Contains("productsArray", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/stock", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/availability", StringComparison.OrdinalIgnoreCase)
            || url.Contains("seo/config", StringComparison.OrdinalIgnoreCase)
            || (url.Contains("/itxrest/", StringComparison.OrdinalIgnoreCase) && url.Contains("/product", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> BuildCandidates(BrandConfig cfg, ProductRef? p)
    {
        if (p is null) yield break;

        switch (cfg.Brand)
        {
            case Brand.Zara:
                yield return $"{cfg.Origin}/tr/tr/products-details?productIds={p.ProductId}&ajax=true";
                break;

            case Brand.Bershka:
                yield return $"{cfg.Origin}/itxrest/2/catalog/store/{cfg.StoreId}/{cfg.RegionId}/product/{p.ProductId}/stock?languageId={cfg.LanguageId}&appId=1";
                yield return $"{cfg.Origin}/itxrest/3/catalog/store/{cfg.StoreId}/{cfg.RegionId}/productsArray?productIds={p.ProductId}&languageId={cfg.LanguageId}&appId=1";
                break;

            case Brand.Stradivarius:
                yield return $"{cfg.Origin}/itxrest/2/web/seo/config?appId=1";
                break;
        }
    }

    private static string AbckShort(string? abck)
    {
        if (string.IsNullOrEmpty(abck)) return "(yok)";
        var validated = !abck.Contains("~-1~");
        return $"{(validated ? "DOĞRULANMIŞ" : "doğrulanmamış")} ({Trunc(abck, 40)})";
    }

    private static string Trunc(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "(null)";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= max ? s : s.Substring(0, max) + $"...(+{s.Length - max})";
    }
}
