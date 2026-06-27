using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LoadStock.Core.Fetch;
using LoadStock.Core.Model;

namespace LoadStock.Core.Brands;

public sealed class ZaraClient : IBrandClient
{
    private readonly IWebFetcher _fetcher;
    private readonly BrandConfig _cfg = BrandCatalog.Zara;

    public ZaraClient(IWebFetcher fetcher) => _fetcher = fetcher;

    public Brand Brand => Brand.Zara;

    public bool TryParseUrl(string pastedUrl, [NotNullWhen(true)] out ProductRef? productRef)
        => UrlParsing.TryParseZara(pastedUrl, out productRef);

    public async Task<ProductInfo> FetchInfoAsync(ProductRef p, CancellationToken ct)
    {
        var url = $"{_cfg.Origin}/tr/tr/products-details?productIds={p.ProductId}&ajax=true";
        var res = await _fetcher.FetchJsonAsync(_cfg.Origin, url, ct);
        BrandClientHelpers.EnsureOk(res, url);

        using var doc = JsonDocument.Parse(res.Body);
        var root = doc.RootElement;
        var product = root.ValueKind == JsonValueKind.Array
            ? (root.GetArrayLength() > 0 ? root[0] : throw new InvalidOperationException($"Zara ürün bulunamadı: {p.ProductId}"))
            : root;

        var name = ItxParsing.Str(product, "name") ?? string.Empty;
        var sizes = new List<SizeInfo>();

        if (product.TryGetProperty("detail", out var detail)
            && detail.TryGetProperty("colors", out var colors) && colors.ValueKind == JsonValueKind.Array)
        {
            foreach (var color in colors.EnumerateArray())
            {
                // v1 katalog kimliği belirli bir renge karşılık gelir; o rengi seç.
                var colorProductId = color.TryGetProperty("productId", out var cp) ? ItxParsing.IdString(cp) : null;
                if (!p.NeedsCatalogResolve && colorProductId is not null
                    && !string.Equals(colorProductId, p.ProductId, StringComparison.Ordinal))
                    continue;

                var colorId = ItxParsing.Str(color, "id");
                var colorName = ItxParsing.Str(color, "name");
                if (!color.TryGetProperty("sizes", out var sizesEl)) continue;
                foreach (var s in sizesEl.EnumerateArray())
                {
                    var sku = s.TryGetProperty("sku", out var skuEl) ? ItxParsing.IdString(skuEl) : ItxParsing.IdString(s.GetProperty("id"));
                    sizes.Add(new SizeInfo(
                        SizeId: sku,
                        Label: ItxParsing.Str(s, "name") ?? string.Empty,
                        PriceMinor: ItxParsing.ParseMinor(s, "price"),
                        ColorId: colorId,
                        ColorName: colorName));
                }
            }
        }

        return new ProductInfo(p, name, sizes);
    }

    public async Task<IReadOnlyList<SizeAvailability>> FetchAvailabilityAsync(ProductRef p, CancellationToken ct)
    {
        var url = $"{_cfg.Origin}/itxrest/1/catalog/store/{_cfg.StoreId}/product/id/{p.ProductId}/availability";
        var res = await _fetcher.FetchJsonAsync(_cfg.Origin, url, ct);
        BrandClientHelpers.EnsureOk(res, url);

        using var doc = JsonDocument.Parse(res.Body);
        var result = new List<SizeAvailability>();
        if (!doc.RootElement.TryGetProperty("skusAvailability", out var skus) || skus.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var s in skus.EnumerateArray())
        {
            var availability = ItxParsing.Str(s, "availability") ?? "unknown";
            var inStock = string.Equals(availability, "in_stock", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(availability, "low_on_stock", StringComparison.OrdinalIgnoreCase);
            result.Add(new SizeAvailability(
                SizeId: ItxParsing.IdString(s.GetProperty("sku")),
                SizeLabel: string.Empty,
                InStock: inStock,
                LowStock: string.Equals(availability, "low_on_stock", StringComparison.OrdinalIgnoreCase),
                PriceMinor: null,
                RawState: availability));
        }

        return result;
    }
}
