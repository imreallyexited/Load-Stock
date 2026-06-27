using LoadStock.Core.Data;
using LoadStock.Core.Diff;
using LoadStock.Core.Model;

namespace LoadStock.App.Services;

/// <summary>
/// Tek bir ürünü kontrol eder: güncel stoğu getir, son durumla diff'le, kaydet ve gerçek
/// restok varsa bildir. Hem arka plan yoklayıcı hem de "şimdi kontrol et" UI eylemi bunu kullanır.
/// </summary>
public sealed class ProductPoller
{
    private readonly StockStore _store;
    private readonly BrandClientResolver _resolver;
    private readonly INotifier _notifier;

    public ProductPoller(StockStore store, BrandClientResolver resolver, INotifier notifier)
    {
        _store = store;
        _resolver = resolver;
        _notifier = notifier;
    }

    /// <summary>Ürünü kontrol eder ve tetiklenen restok beden sayısını döndürür.</summary>
    public async Task<int> PollAsync(TrackedProduct product, CancellationToken ct)
    {
        var client = _resolver.Get(product.Brand);
        var pref = ToRef(product);

        var availability = await client.FetchAvailabilityAsync(pref, ct);

        var prior = _store.GetLastStates(product.Id).ToDictionary(kv => kv.Key, kv => kv.Value.InStock);
        var watched = _store.GetWatchedSizes(product.Id).ToHashSet();
        var events = StateDiffer.Diff(prior, availability, product.WatchAnySize, watched);

        var labels = _store.GetSizeInfos(product.Id).ToDictionary(s => s.SizeId);
        var tuples = events.Select(e =>
        {
            labels.TryGetValue(e.SizeId, out var si);
            var label = !string.IsNullOrEmpty(si?.Label)
                ? si!.Label
                : (string.IsNullOrEmpty(e.Current.SizeLabel) ? e.SizeId : e.Current.SizeLabel);
            var price = si?.PriceMinor ?? e.Current.PriceMinor;
            return (e.SizeId, (string?)label, price);
        }).ToList();

        _store.ApplyPoll(product.Id, availability, tuples);

        if (tuples.Count > 0)
        {
            var sizes = tuples.Select(t => new RestockedSize(t.SizeId, t.Item2, t.price)).ToList();
            _notifier.NotifyRestock(new RestockNotification(product, sizes));
        }

        return tuples.Count;
    }

    public static ProductRef ToRef(TrackedProduct p)
        => new(p.Brand, p.ProductId, p.SeoUrl, string.IsNullOrEmpty(p.ColorId) ? null : p.ColorId);
}
