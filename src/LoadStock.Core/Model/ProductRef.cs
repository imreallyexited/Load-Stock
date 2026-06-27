namespace LoadStock.Core.Model;

/// <summary>
/// Bir ürünün takip için ihtiyaç duyulan kimliği. Yapıştırılan üründen üretilir.
/// </summary>
/// <param name="Brand">Hangi marka.</param>
/// <param name="ProductId">Stok sorgusunda kullanılan ürün/katalog kimliği.</param>
/// <param name="SeoUrl">Ürün sayfasının kanonik adresi (bildirime tıklanınca açılır, gerekiyorsa katalog kimliği buradan çözülür).</param>
/// <param name="ColorId">Renk varyantı seçici (Bershka). İsteğe bağlı.</param>
/// <param name="NeedsCatalogResolve">
/// Zara için: URL'de v1 katalog kimliği yoksa true olur; bu durumda gerçek katalog kimliği sayfadan çözülmelidir.
/// </param>
public sealed record ProductRef(
    Brand Brand,
    string ProductId,
    string SeoUrl,
    string? ColorId = null,
    bool NeedsCatalogResolve = false);
