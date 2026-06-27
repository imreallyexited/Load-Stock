using LoadStock.Core.Model;

namespace LoadStock.Core.Diff;

/// <summary>Bir bedenin önceki duruma göre yeni stoğa girişi.</summary>
public sealed record RestockEvent(string SizeId, SizeAvailability Current);

/// <summary>
/// Yalnızca GERÇEK "stokta değil → stokta" geçişlerini bildirim olayı sayar.
/// İlk kez görülen (önceki durumu olmayan) stokta beden tetiklemez — böylece bir ürün
/// eklendiğinde, zaten stokta olan bedenler için bildirim seli oluşmaz.
/// </summary>
public static class StateDiffer
{
    public static bool IsRestock(bool? priorInStock, bool currentInStock)
        => priorInStock == false && currentInStock;

    /// <param name="priorInStock">Beden kimliği → önceki "stokta mı" durumu. Eksik anahtar = ilk görülme.</param>
    /// <param name="current">Bu poll'daki beden durumları.</param>
    /// <param name="watchAnySize">true ise tüm bedenler izlenir; false ise yalnızca watchedSizeIds.</param>
    /// <param name="watchedSizeIds">İzlenecek beden kimlikleri (watchAnySize false ise).</param>
    public static IReadOnlyList<RestockEvent> Diff(
        IReadOnlyDictionary<string, bool> priorInStock,
        IReadOnlyList<SizeAvailability> current,
        bool watchAnySize,
        IReadOnlySet<string> watchedSizeIds)
    {
        var events = new List<RestockEvent>();
        foreach (var cur in current)
        {
            var watched = watchAnySize || watchedSizeIds.Contains(cur.SizeId);
            if (!watched)
                continue;

            bool? prior = priorInStock.TryGetValue(cur.SizeId, out var p) ? p : null;
            if (IsRestock(prior, cur.InStock))
                events.Add(new RestockEvent(cur.SizeId, cur));
        }

        return events;
    }
}
