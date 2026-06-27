using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LoadStock.Core.Fetch;
using LoadStock.Core.Model;

namespace LoadStock.Core.Brands;

public sealed class StradivariusClient : IBrandClient
{
    private readonly IWebFetcher _fetcher;
    private readonly BrandConfig _cfg = BrandCatalog.Stradivarius;

    public StradivariusClient(IWebFetcher fetcher) => _fetcher = fetcher;

    public Brand Brand => Brand.Stradivarius;

    public bool TryParseUrl(string pastedUrl, [NotNullWhen(true)] out ProductRef? productRef)
        => UrlParsing.TryParseStradivarius(pastedUrl, out productRef);

    public async Task<ProductInfo> FetchInfoAsync(ProductRef p, CancellationToken ct)
    {
        var product = await FetchDetailAsync(p, ct);
        var name = ItxParsing.Str(product, "name") ?? string.Empty;

        var sizes = new List<SizeInfo>();
        foreach (var (colorId, colorName, size) in EnumerateSizes(product, p.ColorId))
        {
            sizes.Add(new SizeInfo(
                SizeId: ItxParsing.IdString(size.GetProperty("sku")),
                Label: ItxParsing.Str(size, "name") ?? string.Empty,
                PriceMinor: ItxParsing.ParseMinor(size, "price"),
                ColorId: colorId,
                ColorName: colorName));
        }

        return new ProductInfo(p, name, sizes);
    }

    public async Task<IReadOnlyList<SizeAvailability>> FetchAvailabilityAsync(ProductRef p, CancellationToken ct)
    {
        var product = await FetchDetailAsync(p, ct);

        var result = new List<SizeAvailability>();
        foreach (var (_, _, size) in EnumerateSizes(product, p.ColorId))
        {
            // Stradivarius'ta isBuyable her zaman true gelir; gerçek stok sinyali visibilityValue'dur.
            var visibility = (ItxParsing.Str(size, "visibilityValue") ?? "UNKNOWN").ToUpperInvariant();
            var isBuyable = ItxParsing.Bool(size, "isBuyable");
            var soldOut = visibility is "SOLD_OUT" or "COMING_SOON" or "BACK_SOON" or "HIDDEN" or "NONE" or "UNAVAILABLE";
            result.Add(new SizeAvailability(
                SizeId: ItxParsing.IdString(size.GetProperty("sku")),
                SizeLabel: ItxParsing.Str(size, "name") ?? string.Empty,
                InStock: isBuyable && !soldOut,
                LowStock: visibility == "RUNNING_OUT",
                PriceMinor: ItxParsing.ParseMinor(size, "price"),
                RawState: visibility));
        }

        return result;
    }

    private async Task<JsonDocument> FetchDetailDocAsync(ProductRef p, CancellationToken ct)
    {
        var url = $"{_cfg.Origin}/itxrest/2/catalog/store/{_cfg.StoreId}/{_cfg.CatalogId}/category/0/product/{p.ProductId}/detail" +
                  $"?languageId={_cfg.LanguageId}&appId=1";
        var res = await _fetcher.FetchJsonAsync(_cfg.Origin, url, ct);
        BrandClientHelpers.EnsureOk(res, url);
        return JsonDocument.Parse(res.Body);
    }

    // Not: JsonDocument'i çağıran dispose etmeli. Basitlik için kök elemanı klonlayıp döndürüyoruz.
    private async Task<JsonElement> FetchDetailAsync(ProductRef p, CancellationToken ct)
    {
        using var doc = await FetchDetailDocAsync(p, ct);
        return doc.RootElement.Clone();
    }

    /// <summary>İstenen renge ait bedenleri verir; o renk bulunamazsa tüm renkleri verir (boş kalmasın).</summary>
    private static IEnumerable<(string? colorId, string? colorName, JsonElement size)> EnumerateSizes(JsonElement product, string? wantedColorId)
    {
        if (!ItxParsing.TryGetColors(product, out var colors))
            yield break;

        var matchedAny = false;
        if (wantedColorId is not null)
        {
            foreach (var color in colors.EnumerateArray())
            {
                var colorId = ItxParsing.Str(color, "id");
                if (!string.Equals(colorId, wantedColorId, StringComparison.OrdinalIgnoreCase))
                    continue;
                matchedAny = true;
                var colorName = ItxParsing.Str(color, "name");
                if (color.TryGetProperty("sizes", out var sizesEl))
                    foreach (var s in sizesEl.EnumerateArray())
                        yield return (colorId, colorName, s);
            }
        }

        if (wantedColorId is null || !matchedAny)
        {
            foreach (var color in colors.EnumerateArray())
            {
                var colorId = ItxParsing.Str(color, "id");
                var colorName = ItxParsing.Str(color, "name");
                if (color.TryGetProperty("sizes", out var sizesEl))
                    foreach (var s in sizesEl.EnumerateArray())
                        yield return (colorId, colorName, s);
            }
        }
    }
}
