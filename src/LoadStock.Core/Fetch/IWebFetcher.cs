namespace LoadStock.Core.Fetch;

/// <summary>
/// Belirli bir origin (marka) bağlamında bir JSON adresini getiren soyutlama.
/// Gerçekleştirimi (WebView2) Core dışındadır; Core yalnızca bu dikişe bağlıdır.
/// </summary>
public interface IWebFetcher
{
    /// <param name="origin">Marka kökeni, ör. https://www.bershka.com</param>
    /// <param name="url">Tam istek adresi (origin ile aynı kökende olmalı).</param>
    Task<FetchResult> FetchJsonAsync(string origin, string url, CancellationToken ct);
}

public sealed record FetchResult(int Status, string Body)
{
    public bool IsSuccess => Status is >= 200 and < 300;

    /// <summary>Akamai/bot engeli (genelde 403 veya "Access Denied").</summary>
    public bool IsBlocked => Status == 403
        || Body.Contains("Access Denied", StringComparison.OrdinalIgnoreCase);
}
