using LoadStock.Core.Model;

namespace LoadStock.Core.Brands;

/// <summary>
/// Marka başına, ülkeye/store'a özgü yapılandırma. Belgelenmemiş ve zamanla değişebilen
/// değerler tek bir yerde tutulur ki bir kırılma tek satırlık düzeltme olsun.
/// "Verified=false" olan alanlar canlı siteden (F12 → Network) doğrulanmalıdır.
/// </summary>
public sealed record BrandConfig(
    Brand Brand,
    string Origin,
    string WarmNavUrl,
    string Locale,
    string Currency,
    string ItxVersion,
    string? StoreId = null,
    string? RegionId = null,
    string? CatalogId = null,
    string? LanguageId = null,
    bool StoreIdDynamic = false,
    bool Verified = false);

/// <summary>Üç marka için Türkiye yapılandırma tablosu.</summary>
public static class BrandCatalog
{
    // Zara: ürün bilgisi products-details ajax akışıyla (/tr/tr/), canlı stok ise
    // itxrest/1 .../store/{store}/product/id/{id}/availability ile okunur. TR store=11766.
    // (Canlı doğrulandı: 2026-06-27.)
    public static readonly BrandConfig Zara = new(
        Brand: Brand.Zara,
        Origin: "https://www.zara.com",
        WarmNavUrl: "https://www.zara.com/tr/tr/",
        Locale: "tr-TR",
        Currency: "TRY",
        ItxVersion: "1",
        StoreId: "11766",
        Verified: true);

    // Bershka: ürün bilgisi itxrest/3 productsArray, canlı stok itxrest/2 .../product/{id}/stock.
    // TR store/region/languageId canlı doğrulandı (2026-06-27).
    public static readonly BrandConfig Bershka = new(
        Brand: Brand.Bershka,
        Origin: "https://www.bershka.com",
        WarmNavUrl: "https://www.bershka.com/tr/",
        Locale: "tr_TR",
        Currency: "TRY",
        ItxVersion: "2",
        StoreId: "44109521",
        RegionId: "40259537",
        LanguageId: "-43",
        Verified: true);

    // Stradivarius: itxrest/2 .../{store}/{catalog}/category/0/product/{id}/detail.
    // TR store=54009571, catalog=50331068 (kadın bölümünde doğrulandı). Bölüme göre değişebilir;
    // gerekirse /itxrest/2/web/seo/config ile dinamik çözülür (StoreIdDynamic). productId URL'de "pelement".
    public static readonly BrandConfig Stradivarius = new(
        Brand: Brand.Stradivarius,
        Origin: "https://www.stradivarius.com",
        WarmNavUrl: "https://www.stradivarius.com/tr/",
        Locale: "tr-TR",
        Currency: "TRY",
        ItxVersion: "2",
        StoreId: "54009571",
        CatalogId: "50331068",
        LanguageId: "-43",
        StoreIdDynamic: true,
        Verified: true);

    public static BrandConfig For(Brand brand) => brand switch
    {
        Brand.Zara => Zara,
        Brand.Bershka => Bershka,
        Brand.Stradivarius => Stradivarius,
        _ => throw new ArgumentOutOfRangeException(nameof(brand), brand, null),
    };

    public static IReadOnlyList<BrandConfig> All { get; } = new[] { Zara, Bershka, Stradivarius };
}
