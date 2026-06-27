using System.Diagnostics.CodeAnalysis;
using LoadStock.Core.Model;

namespace LoadStock.Core.Brands;

/// <summary>Tek bir markanın URL ayrıştırma + stok getirme davranışı.</summary>
public interface IBrandClient
{
    Brand Brand { get; }

    /// <summary>Yapıştırılan adresi ayrıştırır. Bu markaya ait değilse false döner; asla istisna atmaz.</summary>
    bool TryParseUrl(string pastedUrl, [NotNullWhen(true)] out ProductRef? productRef);

    /// <summary>
    /// Ürün adını ve beden/renk/fiyat bilgisini getirir (ekleme anında; göreceli olarak ağır çağrı).
    /// Etiketler önbelleğe yazılır ki poll sırasında yalnızca hafif availability çağrısı yapılsın.
    /// </summary>
    Task<ProductInfo> FetchInfoAsync(ProductRef productRef, CancellationToken ct);

    /// <summary>Beden bazlı güncel stok durumunu getirir (poll; mümkün olduğunca hafif çağrı).</summary>
    Task<IReadOnlyList<SizeAvailability>> FetchAvailabilityAsync(ProductRef productRef, CancellationToken ct);
}
