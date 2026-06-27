namespace LoadStock.Core.Model;

/// <summary>Bir ürünün tek bir bedeninin stok durumu (markalar arası normalleştirilmiş).</summary>
/// <param name="SizeId">Markanın beden kimliği (kalıcı durum karşılaştırmasının anahtarı).</param>
/// <param name="SizeLabel">Görünen beden adı (S, M, 38 vb.).</param>
/// <param name="InStock">Satın alınabilir mi (in_stock veya low_on_stock).</param>
/// <param name="LowStock">Stok azalıyor işareti.</param>
/// <param name="PriceMinor">Fiyat, küçük birim (kuruş) cinsinden; bilinmiyorsa null.</param>
/// <param name="RawState">Markadan gelen ham durum metni (teşhis için saklanır).</param>
public sealed record SizeAvailability(
    string SizeId,
    string SizeLabel,
    bool InStock,
    bool LowStock,
    long? PriceMinor,
    string RawState);
