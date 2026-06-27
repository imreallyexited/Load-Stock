using LoadStock.Core.Brands;
using LoadStock.Core.Model;

namespace LoadStock.App.Services;

/// <summary>Markaya göre ilgili istemciyi döndürür ve yapıştırılan URL'yi eşleştirir.</summary>
public sealed class BrandClientResolver
{
    private readonly IReadOnlyList<IBrandClient> _clients;

    public BrandClientResolver(IEnumerable<IBrandClient> clients)
    {
        _clients = clients.ToList();
    }

    public IReadOnlyList<IBrandClient> All => _clients;

    public IBrandClient Get(Brand brand)
        => _clients.First(c => c.Brand == brand);

    public IBrandClient? Match(string url, out ProductRef? productRef)
    {
        foreach (var c in _clients)
            if (c.TryParseUrl(url, out productRef))
                return c;
        productRef = null;
        return null;
    }
}
