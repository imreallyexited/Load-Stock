using LoadStock.Core.Model;

namespace LoadStock.Core.Data;

// Not: Dapper'ın sütun→property eşlemesi (MatchNamesWithUnderscores) için bu tipler
// parametresiz kurucuya ve yazılabilir property'lere sahip sınıflardır (positional record değil).

/// <summary>İzlenen bir ürün (DB satırı).</summary>
public sealed class TrackedProduct
{
    public long Id { get; set; }
    public Brand Brand { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string? ColorId { get; set; }
    public string SeoUrl { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool WatchAnySize { get; set; }
    public bool Paused { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}

/// <summary>Bir bedenin son bilinen durumu (diff'in temeli).</summary>
public sealed class LastState
{
    public string SizeId { get; set; } = string.Empty;
    public bool InStock { get; set; }
    public bool LowStock { get; set; }
    public long? PriceMinor { get; set; }
    public string? RawState { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
}

/// <summary>Stoğa giriş olayı (geçmiş listesi için, ürün bilgisiyle birleştirilmiş).</summary>
public sealed class StockEventRow
{
    public long Id { get; set; }
    public long ProductPk { get; set; }
    public string SizeId { get; set; } = string.Empty;
    public string? SizeLabel { get; set; }
    public string EventType { get; set; } = string.Empty;
    public long? PriceMinor { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? ProductName { get; set; }
    public Brand Brand { get; set; }
    public string SeoUrl { get; set; } = string.Empty;
}
