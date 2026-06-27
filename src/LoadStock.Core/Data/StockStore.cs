using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using LoadStock.Core.Model;

namespace LoadStock.Core.Data;

/// <summary>
/// SQLite tabanlı kalıcı depo: izlenen ürünler, izlenen bedenler, son durum ve stok olayları.
/// Bağlantılar işlem başına açılır (SQLite + WAL ile güvenli). Şema açılışta oluşturulur.
/// </summary>
public sealed class StockStore
{
    private readonly string _connString;

    static StockStore()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
    }

    public StockStore(string dbPath)
    {
        _connString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        using var con = Open();
        con.Execute("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;");
        con.Execute(SchemaSql);
    }

    private SqliteConnection Open()
    {
        var con = new SqliteConnection(_connString);
        con.Open();
        con.Execute("PRAGMA foreign_keys=ON;");
        return con;
    }

    // ---- ürünler ----

    public long AddOrUpdateProduct(Brand brand, string productId, string? colorId, string seoUrl, string? name, bool watchAnySize)
    {
        using var con = Open();
        const string sql = """
            INSERT INTO tracked_product (brand, product_id, color_id, seo_url, name, watch_any_size, paused, added_at)
            VALUES (@brand, @productId, @colorId, @seoUrl, @name, @watchAnySize, 0, @addedAt)
            ON CONFLICT(brand, product_id, color_id) DO UPDATE SET
                seo_url = excluded.seo_url,
                name = COALESCE(excluded.name, tracked_product.name),
                watch_any_size = excluded.watch_any_size
            RETURNING id;
            """;
        return con.ExecuteScalar<long>(sql, new
        {
            brand = (int)brand,
            productId,
            colorId = colorId ?? string.Empty,
            seoUrl,
            name,
            watchAnySize,
            addedAt = DateTimeOffset.Now,
        });
    }

    public IReadOnlyList<TrackedProduct> GetProducts()
    {
        using var con = Open();
        return con.Query<TrackedProduct>("SELECT * FROM tracked_product ORDER BY added_at DESC;").AsList();
    }

    public TrackedProduct? GetProduct(long id)
    {
        using var con = Open();
        return con.QuerySingleOrDefault<TrackedProduct>("SELECT * FROM tracked_product WHERE id=@id;", new { id });
    }

    public void RemoveProduct(long id)
    {
        using var con = Open();
        con.Execute("DELETE FROM tracked_product WHERE id=@id;", new { id });
    }

    public void SetPaused(long id, bool paused)
    {
        using var con = Open();
        con.Execute("UPDATE tracked_product SET paused=@paused WHERE id=@id;", new { id, paused });
    }

    public void UpdateName(long id, string name)
    {
        using var con = Open();
        con.Execute("UPDATE tracked_product SET name=@name WHERE id=@id;", new { id, name });
    }

    // ---- izlenen bedenler ----

    public IReadOnlyList<string> GetWatchedSizes(long productPk)
    {
        using var con = Open();
        return con.Query<string>("SELECT size_id FROM watched_size WHERE product_pk=@pk;", new { pk = productPk }).AsList();
    }

    public void SetWatchedSizes(long productPk, IEnumerable<string> sizeIds)
    {
        using var con = Open();
        using var tx = con.BeginTransaction();
        con.Execute("DELETE FROM watched_size WHERE product_pk=@pk;", new { pk = productPk }, tx);
        foreach (var sid in sizeIds.Distinct())
            con.Execute("INSERT OR IGNORE INTO watched_size (product_pk, size_id) VALUES (@pk, @sid);",
                new { pk = productPk, sid }, tx);
        tx.Commit();
    }

    // ---- beden bilgisi (etiket/fiyat/renk; ekleme anında doldurulur) ----

    public void SetSizeInfos(long productPk, IEnumerable<SizeInfo> sizes)
    {
        using var con = Open();
        using var tx = con.BeginTransaction();
        con.Execute("DELETE FROM size_info WHERE product_pk=@pk;", new { pk = productPk }, tx);
        const string ins = """
            INSERT OR REPLACE INTO size_info (product_pk, size_id, label, color_id, color_name, price_minor)
            VALUES (@pk, @sid, @label, @cid, @cname, @price);
            """;
        foreach (var s in sizes)
            con.Execute(ins, new { pk = productPk, sid = s.SizeId, label = s.Label, cid = s.ColorId, cname = s.ColorName, price = s.PriceMinor }, tx);
        tx.Commit();
    }

    public IReadOnlyList<SizeInfo> GetSizeInfos(long productPk)
    {
        using var con = Open();
        var rows = con.Query<SizeInfoRecord>(
            "SELECT size_id, label, color_id, color_name, price_minor FROM size_info WHERE product_pk=@pk;",
            new { pk = productPk });
        return rows.Select(r => new SizeInfo(r.SizeId, r.Label ?? string.Empty, r.PriceMinor, r.ColorId, r.ColorName)).ToList();
    }

    // ---- son durum + diff uygula ----

    public IReadOnlyDictionary<string, LastState> GetLastStates(long productPk)
    {
        using var con = Open();
        var rows = con.Query<LastState>(
            "SELECT size_id, in_stock, low_stock, price_minor, raw_state, checked_at FROM last_state WHERE product_pk=@pk;",
            new { pk = productPk });
        return rows.ToDictionary(r => r.SizeId, r => r);
    }

    /// <summary>Poll sonucunu TEK transaction'da uygular: son durumu günceller ve olayları yazar.</summary>
    public void ApplyPoll(long productPk, IReadOnlyList<SizeAvailability> current, IReadOnlyList<(string SizeId, string? Label, long? PriceMinor)> restockEvents)
    {
        var now = DateTimeOffset.Now;
        using var con = Open();
        using var tx = con.BeginTransaction();

        const string upsert = """
            INSERT INTO last_state (product_pk, size_id, in_stock, low_stock, price_minor, raw_state, checked_at)
            VALUES (@pk, @sid, @in, @low, @price, @raw, @at)
            ON CONFLICT(product_pk, size_id) DO UPDATE SET
                in_stock = excluded.in_stock, low_stock = excluded.low_stock,
                price_minor = excluded.price_minor, raw_state = excluded.raw_state, checked_at = excluded.checked_at;
            """;
        foreach (var s in current)
            con.Execute(upsert, new
            {
                pk = productPk, sid = s.SizeId, @in = s.InStock, low = s.LowStock,
                price = s.PriceMinor, raw = s.RawState, at = now,
            }, tx);

        const string insertEvent = """
            INSERT INTO stock_event (product_pk, size_id, size_label, event_type, price_minor, occurred_at)
            VALUES (@pk, @sid, @label, 'restock', @price, @at);
            """;
        foreach (var e in restockEvents)
            con.Execute(insertEvent, new { pk = productPk, sid = e.SizeId, label = e.Label, price = e.PriceMinor, at = now }, tx);

        tx.Commit();
    }

    // ---- geçmiş ----

    public IReadOnlyList<StockEventRow> GetRecentEvents(int limit = 200)
    {
        using var con = Open();
        const string sql = """
            SELECT e.id, e.product_pk, e.size_id, e.size_label, e.event_type, e.price_minor, e.occurred_at,
                   p.name AS product_name, p.brand, p.seo_url
            FROM stock_event e
            JOIN tracked_product p ON p.id = e.product_pk
            ORDER BY e.occurred_at DESC
            LIMIT @limit;
            """;
        return con.Query<StockEventRow>(sql, new { limit }).AsList();
    }

    // ---- ayarlar ----

    public string? GetSetting(string key)
    {
        using var con = Open();
        return con.QuerySingleOrDefault<string>("SELECT value FROM app_setting WHERE key=@key;", new { key });
    }

    public void SetSetting(string key, string value)
    {
        using var con = Open();
        con.Execute("""
            INSERT INTO app_setting (key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """, new { key, value });
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS tracked_product (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            brand          INTEGER NOT NULL,
            product_id     TEXT    NOT NULL,
            color_id       TEXT    NOT NULL DEFAULT '',
            seo_url        TEXT    NOT NULL,
            name           TEXT,
            watch_any_size INTEGER NOT NULL DEFAULT 1,
            paused         INTEGER NOT NULL DEFAULT 0,
            added_at       TEXT    NOT NULL,
            UNIQUE(brand, product_id, color_id)
        );
        CREATE TABLE IF NOT EXISTS watched_size (
            product_pk INTEGER NOT NULL REFERENCES tracked_product(id) ON DELETE CASCADE,
            size_id    TEXT    NOT NULL,
            PRIMARY KEY (product_pk, size_id)
        );
        CREATE TABLE IF NOT EXISTS last_state (
            product_pk  INTEGER NOT NULL REFERENCES tracked_product(id) ON DELETE CASCADE,
            size_id     TEXT    NOT NULL,
            in_stock    INTEGER NOT NULL,
            low_stock   INTEGER NOT NULL DEFAULT 0,
            price_minor INTEGER,
            raw_state   TEXT,
            checked_at  TEXT    NOT NULL,
            PRIMARY KEY (product_pk, size_id)
        );
        CREATE TABLE IF NOT EXISTS stock_event (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            product_pk  INTEGER NOT NULL REFERENCES tracked_product(id) ON DELETE CASCADE,
            size_id     TEXT    NOT NULL,
            size_label  TEXT,
            event_type  TEXT    NOT NULL,
            price_minor INTEGER,
            occurred_at TEXT    NOT NULL
        );
        CREATE TABLE IF NOT EXISTS size_info (
            product_pk  INTEGER NOT NULL REFERENCES tracked_product(id) ON DELETE CASCADE,
            size_id     TEXT    NOT NULL,
            label       TEXT,
            color_id    TEXT,
            color_name  TEXT,
            price_minor INTEGER,
            PRIMARY KEY (product_pk, size_id)
        );
        CREATE TABLE IF NOT EXISTS app_setting (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_event_time ON stock_event(occurred_at DESC);
        """;
}

// Dapper okuması için yazılabilir property'li yardımcı (positional record değil).
internal sealed class SizeInfoRecord
{
    public string SizeId { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? ColorId { get; set; }
    public string? ColorName { get; set; }
    public long? PriceMinor { get; set; }
}

internal sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value)
        => DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString("o", CultureInfo.InvariantCulture);
    }
}
