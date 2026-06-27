namespace LoadStock.Core.Model;

/// <summary>Bir ürünün belirli bir andaki tüm bedenlerinin stok görüntüsü.</summary>
public sealed record ProductSnapshot(
    ProductRef Ref,
    string Name,
    IReadOnlyList<SizeAvailability> Sizes,
    DateTimeOffset FetchedAt);
