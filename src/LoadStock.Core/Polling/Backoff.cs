namespace LoadStock.Core.Polling;

/// <summary>403/hata sonrası üssel geri çekilme süresini hesaplar (üst sınırla).</summary>
public static class Backoff
{
    /// <param name="baseSeconds">Normal poll aralığı.</param>
    /// <param name="failCount">Ardışık başarısızlık sayısı (0 = başarılı).</param>
    /// <param name="maxSeconds">Üst sınır.</param>
    public static int NextDelaySeconds(int baseSeconds, int failCount, int maxSeconds)
    {
        if (failCount <= 0)
            return Math.Min(baseSeconds, maxSeconds);

        var scaled = baseSeconds * Math.Pow(2, failCount);
        if (double.IsInfinity(scaled) || scaled > maxSeconds)
            return maxSeconds;
        return (int)scaled;
    }
}
