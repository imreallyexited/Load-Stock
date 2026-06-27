using LoadStock.Core.Fetch;

namespace LoadStock.Core.Brands;

/// <summary>Stok kaynağına erişilemediğinde fırlatılır (engellendi veya hatalı yanıt).</summary>
public sealed class FetchFailedException : Exception
{
    public int Status { get; }
    public bool Blocked { get; }

    public FetchFailedException(string message, int status, bool blocked) : base(message)
    {
        Status = status;
        Blocked = blocked;
    }
}

internal static class BrandClientHelpers
{
    public static void EnsureOk(FetchResult res, string url)
    {
        if (res.IsSuccess && !res.IsBlocked)
            return;
        var reason = res.IsBlocked ? "engellendi (bot koruması)" : $"HTTP {res.Status}";
        throw new FetchFailedException($"İstek başarısız ({reason}): {url}", res.Status, res.IsBlocked);
    }
}
