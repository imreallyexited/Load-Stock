using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LoadStock.Core.Fetch;
using LoadStock.Core.Model;

namespace LoadStock.Core.Brands;

public sealed class BershkaClient : IBrandClient
{
    private readonly IWebFetcher _fetcher;
    private readonly BrandConfig _cfg = BrandCatalog.Bershka;

    public BershkaClient(IWebFetcher fetcher) => _fetcher = fetcher;

    public Brand Brand => Brand.Bershka;

    public bool TryParseUrl(string pastedUrl, [NotNullWhen(true)] out ProductRef? productRef)
        => UrlParsing.TryParseBershka(pastedUrl, out productRef);

    public async Task<ProductInfo> FetchInfoAsync(ProductRef p, CancellationToken ct)
    {
        var url = $"{_cfg.Origin}/itxrest/3/catalog/store/{_cfg.StoreId}/{_cfg.RegionId}/productsArray" +
                  $"?productIds={p.ProductId}&languageId={_cfg.LanguageId}&appId=1";
        var res = await _fetcher.FetchJsonAsync(_cfg.Origin, url, ct);
        BrandClientHelpers.EnsureOk(res, url);

        using var doc = JsonDocument.Parse(res.Body);
        var products = doc.RootElement.GetProperty("products");
        if (products.GetArrayLength() == 0)
            throw new InvalidOperationException($"Bershka ürün bulunamadı: {p.ProductId}");

        var product = products[0];
        var name = ItxParsing.Str(product, "name") ?? string.Empty;

        var sizes = new List<SizeInfo>();
        if (ItxParsing.TryGetColors(product, out var colors))
        {
            foreach (var color in colors.EnumerateArray())
            {
                var colorId = ItxParsing.Str(color, "id");
                if (p.ColorId is not null && !string.Equals(colorId, p.ColorId, StringComparison.OrdinalIgnoreCase))
                    continue;
                var colorName = ItxParsing.Str(color, "name");
                if (!color.TryGetProperty("sizes", out var sizesEl)) continue;
                foreach (var s in sizesEl.EnumerateArray())
                {
                    sizes.Add(new SizeInfo(
                        SizeId: ItxParsing.IdString(s.GetProperty("sku")),
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
        var url = $"{_cfg.Origin}/itxrest/2/catalog/store/{_cfg.StoreId}/{_cfg.RegionId}/product/{p.ProductId}/stock" +
                  $"?languageId={_cfg.LanguageId}&appId=1";
        var res = await _fetcher.FetchJsonAsync(_cfg.Origin, url, ct);
        BrandClientHelpers.EnsureOk(res, url);

        using var doc = JsonDocument.Parse(res.Body);
        var result = new List<SizeAvailability>();
        if (!doc.RootElement.TryGetProperty("stocks", out var outer) || outer.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var group in outer.EnumerateArray())
        {
            if (!group.TryGetProperty("stocks", out var inner) || inner.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var s in inner.EnumerateArray())
            {
                var availability = ItxParsing.Str(s, "availability") ?? "unknown";
                var threshold = ItxParsing.Str(s, "typeThreshold");
                result.Add(new SizeAvailability(
                    SizeId: ItxParsing.IdString(s.GetProperty("id")),
                    SizeLabel: string.Empty,
                    InStock: string.Equals(availability, "in_stock", StringComparison.OrdinalIgnoreCase),
                    LowStock: string.Equals(threshold, "BSK_UMBRAL_BAJO", StringComparison.OrdinalIgnoreCase),
                    PriceMinor: null,
                    RawState: availability));
            }
        }

        return result;
    }
}
