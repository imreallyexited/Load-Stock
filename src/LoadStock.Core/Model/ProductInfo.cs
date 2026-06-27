namespace LoadStock.Core.Model;

/// <summary>Bir bedenin kalıcı bilgisi (etiket, fiyat, renk). Ekleme anında alınır ve önbelleğe yazılır.</summary>
public sealed record SizeInfo(
    string SizeId,
    string Label,
    long? PriceMinor,
    string? ColorId,
    string? ColorName);

/// <summary>Bir ürünün adı ve beden bilgisi (canlı stok hariç). Ekleme anında doldurulur.</summary>
public sealed record ProductInfo(
    ProductRef Ref,
    string Name,
    IReadOnlyList<SizeInfo> Sizes);
