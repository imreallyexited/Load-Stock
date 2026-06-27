using System.Text.Json;

namespace LoadStock.Core.Brands;

/// <summary>Inditex itxrest JSON gövdeleri için ortak ayrıştırma yardımcıları.</summary>
internal static class ItxParsing
{
    public static string IdString(JsonElement el) =>
        el.ValueKind == JsonValueKind.Number ? el.GetInt64().ToString() : (el.GetString() ?? string.Empty);

    /// <summary>Fiyatı küçük birim (kuruş) tamsayısı olarak döndürür. Sayı veya sayısal dize olabilir.</summary>
    public static long? ParseMinor(JsonElement parent, string prop)
    {
        if (!parent.TryGetProperty(prop, out var el))
            return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n))
            return n;
        if (el.ValueKind == JsonValueKind.String && long.TryParse(el.GetString(), out var m))
            return m;
        return null;
    }

    public static string? Str(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v)
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
            : null;

    public static bool Bool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False && v.GetBoolean();

    /// <summary>bundleProductSummaries[0].detail.colors dizisini bulur (yoksa detail.colors).</summary>
    public static bool TryGetColors(JsonElement productObj, out JsonElement colors)
    {
        colors = default;
        if (productObj.TryGetProperty("bundleProductSummaries", out var bps)
            && bps.ValueKind == JsonValueKind.Array && bps.GetArrayLength() > 0
            && bps[0].TryGetProperty("detail", out var det)
            && det.TryGetProperty("colors", out var cols) && cols.ValueKind == JsonValueKind.Array)
        {
            colors = cols;
            return true;
        }

        if (productObj.TryGetProperty("detail", out var det2)
            && det2.TryGetProperty("colors", out var cols2) && cols2.ValueKind == JsonValueKind.Array)
        {
            colors = cols2;
            return true;
        }

        return false;
    }
}
