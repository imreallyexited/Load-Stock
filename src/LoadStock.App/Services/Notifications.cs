using System.Diagnostics;
using LoadStock.Core.Data;

namespace LoadStock.App.Services;

public sealed record RestockedSize(string SizeId, string? Label, long? PriceMinor);

public sealed record RestockNotification(TrackedProduct Product, IReadOnlyList<RestockedSize> Sizes);

/// <summary>Stoğa giriş bildirimi gönderen soyutlama (toast). M5'te gerçekleştirilir.</summary>
public interface INotifier
{
    void NotifyRestock(RestockNotification notification);
}

/// <summary>Geçici/yedek bildirici: yalnızca iz bırakır (toast yokken kullanılır).</summary>
public sealed class LogNotifier : INotifier
{
    public void NotifyRestock(RestockNotification n)
    {
        var sizes = string.Join(", ", n.Sizes.Select(s => s.Label ?? s.SizeId));
        Trace.WriteLine($"[restock] {n.Product.Brand} {n.Product.Name}: {sizes}");
    }
}
