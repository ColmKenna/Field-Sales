using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>The active snapshot; <see cref="CompletedUtc"/> is the rep's "Last synced" time.</summary>
public sealed record ActiveSnapshot(string Generation, DateTimeOffset CompletedUtc, SnapshotManifest Manifest);

public sealed record HomeLocation(Guid Id, string Name, string TownName, string CustomerName);

/// <summary>The hashes the server sent with a download, checked before the snapshot can become active.</summary>
public sealed record ExpectedSnapshot(string GzipSha256, string PlainSha256);

/// <summary>Test-only pause points at the recovery boundaries, as in the spike.</summary>
public sealed class SnapshotHooks
{
    public Action<string>? AtBoundary { get; init; }
}

/// <summary>The rep's Morning Snapshots in SQLite generations (1C, from the spike's verified model).</summary>
/// <remarks>
/// A download is imported as a staged generation and verified (both hashes, rep, version, counts) before one
/// transaction makes it active. The previous active generation is kept; older ones are retired in that same
/// transaction and deleted afterwards in bounded batches (D2-B), so Sync ends at the switch. Opening the store
/// retires any staged generation an interrupted import left behind; <see cref="PurgeRetiredAsync"/> then
/// finishes any deletion still owed, including after the app stopped part-way.
/// </remarks>
public sealed partial class SnapshotStore
{
    public const int RetainedGenerations = 2;
    internal const int BatchRows = 128;
    internal const int PurgeBatchRows = 2000;
    internal const long MaximumPlainBytes = 1L << 30;
    internal static readonly int[] ReadableMajors = [SnapshotContract.CurrentMajor];
    private const int SchemaVersion = 6;
    // Format 6 (snapshot 1.6, T-6.4.2): the tiers the customers of the rep's Locations hold, their terms, and quantity breaks.
    private const string PricingSchema = """
        CREATE TABLE price_tiers(generation TEXT NOT NULL REFERENCES generations(id), id TEXT NOT NULL, name TEXT NOT NULL,
            shape TEXT NOT NULL CHECK(shape IN ('Percentage','PriceList')), percent_off TEXT, PRIMARY KEY(generation, id));
        CREATE TABLE price_tier_terms(generation TEXT NOT NULL REFERENCES generations(id), tier_id TEXT NOT NULL,
            product_id TEXT NOT NULL, percent_off TEXT, fixed_price TEXT, CHECK((percent_off IS NULL) <> (fixed_price IS NULL)),
            PRIMARY KEY(generation, tier_id, product_id));
        CREATE TABLE customer_tiers(generation TEXT NOT NULL REFERENCES generations(id), customer_id TEXT NOT NULL,
            tier_id TEXT NOT NULL, PRIMARY KEY(generation, customer_id, tier_id));
        CREATE TABLE quantity_breaks(generation TEXT NOT NULL REFERENCES generations(id), product_id TEXT NOT NULL,
            from_quantity TEXT NOT NULL, price TEXT NOT NULL, PRIMARY KEY(generation, product_id, from_quantity));
        """;
    // Format 5 (snapshot 1.5, T-5.1.6): each recent order product's quantity on each recent Accepted Order, newest first.
    private const string QuantitySchema = """
        CREATE TABLE recent_order_quantities(generation TEXT NOT NULL REFERENCES generations(id), location_id TEXT NOT NULL,
            product_id TEXT NOT NULL, position INTEGER NOT NULL CHECK(position >= 0), quantity TEXT NOT NULL, unit TEXT NOT NULL,
            PRIMARY KEY(generation, location_id, product_id, position));
        """;
    // Format 4 (snapshot 1.4, T-5.1.5): the Low products a Location's most recent Call left unordered.
    private const string LastCallSchema = """
        CREATE TABLE last_call_low_lines(generation TEXT NOT NULL REFERENCES generations(id), location_id TEXT NOT NULL,
            call_id TEXT NOT NULL, called_on TEXT NOT NULL, product_id TEXT NOT NULL, PRIMARY KEY(generation, location_id, product_id));
        """;
    // Format 3 (snapshot 1.3, T-5.4.1): a Location's last Stock Check lines and the products on its recent Accepted Orders.
    private const string HistorySchema = """
        CREATE TABLE last_stock_check_lines(generation TEXT NOT NULL REFERENCES generations(id), location_id TEXT NOT NULL,
            product_id TEXT NOT NULL, PRIMARY KEY(generation, location_id, product_id));
        CREATE TABLE recent_order_lines(generation TEXT NOT NULL REFERENCES generations(id), location_id TEXT NOT NULL,
            product_id TEXT NOT NULL, PRIMARY KEY(generation, location_id, product_id));
        """;
    private const string AttributeSchema = """
        CREATE TABLE product_attributes(generation TEXT NOT NULL REFERENCES generations(id), product_id TEXT NOT NULL,
            position INTEGER NOT NULL CHECK(position >= 0), name TEXT NOT NULL, value TEXT NOT NULL,
            is_archived INTEGER NOT NULL CHECK(is_archived IN (0,1)), PRIMARY KEY(generation, product_id, position));
        """;
    private const string Schema = """
        CREATE TABLE generations(id TEXT PRIMARY KEY, state TEXT NOT NULL CHECK(state IN ('staged','complete','retired')),
            manifest TEXT NOT NULL, completed TEXT);
        CREATE TABLE active(singleton INTEGER PRIMARY KEY CHECK(singleton=1), generation TEXT NOT NULL REFERENCES generations(id));
        CREATE TABLE locations(generation TEXT NOT NULL REFERENCES generations(id), id TEXT NOT NULL, name TEXT NOT NULL,
            customer_id TEXT NOT NULL, customer_name TEXT NOT NULL, town_id TEXT NOT NULL, town_name TEXT NOT NULL,
            county_name TEXT NOT NULL, eircode TEXT, main_contact TEXT, PRIMARY KEY(generation, id));
        CREATE TABLE categories(generation TEXT NOT NULL REFERENCES generations(id), id TEXT NOT NULL, parent_id TEXT,
            name TEXT NOT NULL, PRIMARY KEY(generation, id));
        CREATE TABLE products(generation TEXT NOT NULL REFERENCES generations(id), id TEXT NOT NULL, code TEXT NOT NULL,
            name TEXT NOT NULL, category_id TEXT NOT NULL, unit TEXT NOT NULL, quantity_step TEXT, minimum_quantity TEXT,
            parent_product_id TEXT, PRIMARY KEY(generation, id));
        CREATE TABLE base_prices(generation TEXT NOT NULL REFERENCES generations(id), product_id TEXT NOT NULL,
            effective_from TEXT NOT NULL, amount TEXT NOT NULL, PRIMARY KEY(generation, product_id, effective_from));
        """;
    // Child tables are emptied before the generation row; the active pointer never names a retired generation.
    private static readonly string[] ChildTables = ["price_tier_terms", "price_tiers", "customer_tiers", "quantity_breaks",
        "recent_order_quantities", "last_call_low_lines", "last_stock_check_lines", "recent_order_lines", "product_attributes", "base_prices",
        "products", "categories", "locations"];
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);
    private readonly RepWorkspace workspace;
    private readonly TimeProvider clock;

    public SnapshotStore(RepWorkspace workspace, TimeProvider? clock = null)
    {
        this.workspace = workspace;
        this.clock = clock ?? TimeProvider.System;
        using SqliteConnection connection = Open();
        Engine = SqliteEngine.Preflight(connection);
        SqliteEngine.Execute(connection, "UPDATE generations SET state='retired' WHERE state='staged'");
    }

    public SqliteEngine Engine { get; }
    internal static SemaphoreSlim GateFor(RepWorkspace workspace) => Gates.GetOrAdd(workspace.SnapshotPath, _ => new(1, 1));
    private SemaphoreSlim Gate => GateFor(workspace);

    private SqliteConnection Open()
    {
        SqliteConnection connection = SqliteEngine.Open(workspace.SnapshotPath);
        try
        {
            using SqliteCommand version = connection.CreateCommand();
            version.CommandText = "PRAGMA user_version";
            // Older formats step up one at a time, keeping every generation; a new database gets the whole schema at once.
            long current = (long)version.ExecuteScalar()!;
            if (current == 1) { SqliteEngine.EnsureSchema(connection, 2, "", AttributeSchema); current = 2; }
            if (current == 2) { SqliteEngine.EnsureSchema(connection, 3, "", HistorySchema); current = 3; }
            if (current == 3) { SqliteEngine.EnsureSchema(connection, 4, "", LastCallSchema); current = 4; }
            if (current == 4) SqliteEngine.EnsureSchema(connection, 5, "", QuantitySchema);
            SqliteEngine.EnsureSchema(connection, SchemaVersion,
                Schema + AttributeSchema + HistorySchema + LastCallSchema + QuantitySchema + PricingSchema, PricingSchema);
        }
        catch { connection.Dispose(); throw; }
        return connection;
    }

    public ActiveSnapshot? ReadActive()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT g.id, g.completed, g.manifest FROM active a JOIN generations g ON g.id=a.generation WHERE g.state='complete'";
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? new(reader.GetString(0), DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                JsonSerializer.Deserialize<SnapshotManifest>(reader.GetString(2), SnapshotContract.JsonOptions)!)
            : null;
    }

    /// <summary>The active snapshot's Locations for Home, by name.</summary>
    public IReadOnlyList<HomeLocation> ReadLocations()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.id, l.name, l.town_name, l.customer_name FROM locations l JOIN active a ON a.generation=l.generation
            ORDER BY l.name COLLATE NOCASE, l.id
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        List<HomeLocation> locations = [];
        while (reader.Read())
            locations.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        return locations;
    }

    /// <summary>Counts rows per generation and state, for verification and diagnostics.</summary>
    internal IReadOnlyList<(string Generation, string State, long Rows)> Generations()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.id, g.state,
                (SELECT COUNT(*) FROM locations WHERE generation=g.id) + (SELECT COUNT(*) FROM categories WHERE generation=g.id)
                + (SELECT COUNT(*) FROM products WHERE generation=g.id) + (SELECT COUNT(*) FROM base_prices WHERE generation=g.id)
                + (SELECT COUNT(*) FROM product_attributes WHERE generation=g.id)
                + (SELECT COUNT(*) FROM last_stock_check_lines WHERE generation=g.id)
                + (SELECT COUNT(*) FROM recent_order_lines WHERE generation=g.id)
                + (SELECT COUNT(*) FROM last_call_low_lines WHERE generation=g.id)
                + (SELECT COUNT(*) FROM recent_order_quantities WHERE generation=g.id)
            FROM generations g ORDER BY g.completed, g.id
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        List<(string, string, long)> rows = [];
        while (reader.Read()) rows.Add((reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        return rows;
    }

    /// <summary>Imports a downloaded snapshot and makes it active only once it is whole and verified.</summary>
    /// <remarks>Storage work runs on a worker thread: the SQLite provider's I/O is synchronous.</remarks>
    public async Task<ActiveSnapshot> ApplyAsync(string gzipPath, ExpectedSnapshot expected, SnapshotHooks? hooks = null,
        CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try { return await Task.Run(() => Apply(gzipPath, expected, hooks ?? new(), ct), ct); }
        finally { Gate.Release(); }
    }

    private ActiveSnapshot Apply(string gzipPath, ExpectedSnapshot expected, SnapshotHooks hooks, CancellationToken ct)
    {
        using (FileStream compressed = File.OpenRead(gzipPath))
            if (Convert.ToHexStringLower(SHA256.HashData(compressed)) != expected.GzipSha256)
                throw new InvalidDataException("The downloaded snapshot does not match its hash.");
        using SqliteConnection connection = Open();
        string generation = Guid.NewGuid().ToString("N");
        SnapshotManifest? manifest = null;
        Dictionary<Guid, SnapshotMainContactReplacement> contactReplacements = [];
        Dictionary<string, int> counts = SnapshotDocumentReader.Families.ToDictionary(family => family, _ => 0);
        SqliteTransaction? batch = null;
        int rows = 0, minor = 0;
        try
        {
            using (FileStream file = File.OpenRead(gzipPath))
            using (GZipStream gzip = new(file, CompressionMode.Decompress))
            using (HashingReadStream plain = new(gzip, MaximumPlainBytes))
            {
                try
                {
                    new SnapshotDocumentReader(plain).Read(received =>
                    {
                        minor = Validate(received);
                        manifest = received;
                        using SqliteCommand create = connection.CreateCommand();
                        create.CommandText = "INSERT INTO generations(id, state, manifest) VALUES($id, 'staged', $manifest)";
                        create.Parameters.AddWithValue("$id", generation);
                        create.Parameters.AddWithValue("$manifest", JsonSerializer.Serialize(received, SnapshotContract.JsonOptions));
                        create.ExecuteNonQuery();
                    }, (family, entity) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        batch ??= connection.BeginTransaction();
                        Insert(connection, batch, generation, minor, family, entity, contactReplacements);
                        counts[family]++;
                        if (++rows % BatchRows == 0)
                        {
                            batch.Commit(); batch.Dispose(); batch = null;
                            if (rows == BatchRows) hooks.AtBoundary?.Invoke("import");
                        }
                    }, ct);
                }
                catch (JsonException malformed) { throw new InvalidDataException("The downloaded snapshot is malformed.", malformed); }
                batch?.Commit(); batch?.Dispose(); batch = null;
                if (plain.FinishHash() != expected.PlainSha256)
                    throw new InvalidDataException("The downloaded snapshot does not match its content hash.");
            }
            SnapshotCounts declared = manifest!.Counts;
            if (declared != new SnapshotCounts(counts["locations"], counts["categories"], counts["products"], counts["basePrices"],
                    counts["productAttributes"], counts["lastStockCheckLines"], counts["recentOrderLines"], counts["lastCallLowLines"],
                    counts["priceTiers"], counts["priceTierTerms"], counts["customerTiers"], counts["quantityBreaks"]))
                throw new InvalidDataException("The downloaded snapshot is incomplete: its counts do not match.");
            using (SqliteCommand references = connection.CreateCommand())
            {
                references.CommandText = """
                    SELECT EXISTS(SELECT 1 FROM product_attributes v LEFT JOIN products p
                        ON p.generation=v.generation AND p.id=v.product_id WHERE v.generation=$g AND p.id IS NULL)
                    """;
                references.Parameters.AddWithValue("$g", generation);
                if ((long)references.ExecuteScalar()! != 0)
                    throw new InvalidDataException("An attribute refers to a product outside this snapshot.");
                // History rows (1.3) and the last Call's gaps (1.4) must name a Location and a product this snapshot carries.
                foreach (string table in (string[])["last_stock_check_lines", "recent_order_lines", "last_call_low_lines"])
                {
                    references.CommandText = $"""
                        SELECT EXISTS(SELECT 1 FROM {table} h
                            LEFT JOIN products p ON p.generation=h.generation AND p.id=h.product_id
                            LEFT JOIN locations l ON l.generation=h.generation AND l.id=h.location_id
                            WHERE h.generation=$g AND (p.id IS NULL OR l.id IS NULL))
                        """;
                    if ((long)references.ExecuteScalar()! != 0)
                        throw new InvalidDataException("A stock history row refers to a Location or product outside this snapshot.");
                }
                references.CommandText = """
                    SELECT EXISTS(SELECT 1 FROM last_call_low_lines WHERE generation=$g
                        GROUP BY location_id HAVING COUNT(DISTINCT call_id) > 1 OR COUNT(DISTINCT called_on) > 1)
                    """;
                if ((long)references.ExecuteScalar()! != 0)
                    throw new InvalidDataException("A Location's last Call gaps name more than one Call.");
                // 1.6: a term names a tier and a product this snapshot carries; a held tier is one it carries; a break's product too.
                references.CommandText = """
                    SELECT EXISTS(SELECT 1 FROM price_tier_terms t
                        LEFT JOIN price_tiers r ON r.generation=t.generation AND r.id=t.tier_id
                        LEFT JOIN products p ON p.generation=t.generation AND p.id=t.product_id
                        WHERE t.generation=$g AND (r.id IS NULL OR p.id IS NULL))
                    OR EXISTS(SELECT 1 FROM customer_tiers c LEFT JOIN price_tiers r ON r.generation=c.generation AND r.id=c.tier_id
                        WHERE c.generation=$g AND r.id IS NULL)
                    OR EXISTS(SELECT 1 FROM quantity_breaks b LEFT JOIN products p ON p.generation=b.generation AND p.id=b.product_id
                        WHERE b.generation=$g AND p.id IS NULL)
                    OR EXISTS(SELECT 1 FROM price_tier_terms t JOIN price_tiers r ON r.generation=t.generation AND r.id=t.tier_id
                        WHERE t.generation=$g AND r.shape='PriceList' AND t.fixed_price IS NULL)
                    """;
                if ((long)references.ExecuteScalar()! != 0)
                    throw new InvalidDataException("A tier, term or break refers to something outside this snapshot.");
            }
            WriteContactReplacements(connection, generation, manifest, contactReplacements);
            // Keep only the old active copy's referenced shop identities before it can be retired.
            // The separate Unsent database is read, never rewritten, by a snapshot replacement.
            LocationRetention.BeforeActivation(workspace, connection);
            hooks.AtBoundary?.Invoke("before-activation");
            ct.ThrowIfCancellationRequested();
            DateTimeOffset completed = clock.GetUtcNow();
            using (SqliteTransaction activation = connection.BeginTransaction())
            using (SqliteCommand activate = connection.CreateCommand())
            {
                activate.Transaction = activation;
                activate.CommandText = $"""
                    UPDATE generations SET state='complete', completed=$when WHERE id=$g;
                    INSERT INTO active(singleton, generation) VALUES(1, $g)
                        ON CONFLICT(singleton) DO UPDATE SET generation=excluded.generation;
                    UPDATE generations SET state='retired' WHERE state='complete' AND id NOT IN
                        (SELECT id FROM generations WHERE state='complete' ORDER BY completed DESC, id DESC LIMIT {RetainedGenerations});
                    """;
                activate.Parameters.AddWithValue("$g", generation);
                activate.Parameters.AddWithValue("$when", completed.ToString("O", CultureInfo.InvariantCulture));
                activate.ExecuteNonQuery();
                activation.Commit();
            }
            hooks.AtBoundary?.Invoke("after-activation");
            return new(generation, completed, manifest);
        }
        catch when (manifest is not null)
        {
            batch?.Dispose(); batch = null;
            // Never usable; deleted with the other retired generations. If storage itself failed, the next
            // open retires it instead, so the original error is never hidden.
            try
            {
                using SqliteCommand retire = connection.CreateCommand();
                retire.CommandText = "UPDATE generations SET state='retired' WHERE id=$g AND state='staged'";
                retire.Parameters.AddWithValue("$g", generation);
                retire.ExecuteNonQuery();
            }
            catch (SqliteException) { }
            throw;
        }
        finally { batch?.Dispose(); }
    }

    /// <summary>Checks the manifest and returns the snapshot's minor version.</summary>
    private int Validate(SnapshotManifest manifest)
    {
        string[] version = (manifest.Version ?? "").Split('.');
        if (manifest.Schema != SnapshotContract.Schema || version.Length != 2
            || !int.TryParse(version[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major)
            || !int.TryParse(version[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor)
            || !ReadableMajors.Contains(major))
            throw new InvalidDataException("This app cannot read the downloaded snapshot version.");
        if (manifest.RepSubject != workspace.RepSubject)
            throw new InvalidDataException("The downloaded snapshot belongs to another rep.");
        if (manifest.Counts is null || manifest.Counts.Locations < 0 || manifest.Counts.Categories < 0
            || manifest.Counts.Products < 0 || manifest.Counts.BasePrices < 0 || manifest.Counts.ProductAttributes < 0
            || manifest.Counts.LastStockCheckLines < 0 || manifest.Counts.RecentOrderLines < 0 || manifest.Counts.LastCallLowLines < 0
            || (minor == 0 && manifest.Counts.ProductAttributes != 0)
            || (minor < 3 && (manifest.Counts.LastStockCheckLines != 0 || manifest.Counts.RecentOrderLines != 0))
            || (minor < 4 && manifest.Counts.LastCallLowLines != 0)
            || manifest.Counts.PriceTiers < 0 || manifest.Counts.PriceTierTerms < 0 || manifest.Counts.CustomerTiers < 0
            || manifest.Counts.QuantityBreaks < 0
            || (minor < 6 && (manifest.Counts.PriceTiers != 0 || manifest.Counts.PriceTierTerms != 0 || manifest.Counts.CustomerTiers != 0
                || manifest.Counts.QuantityBreaks != 0)))
            throw new InvalidDataException("The snapshot manifest is invalid.");
        return minor;
    }

    private static void Insert(SqliteConnection connection, SqliteTransaction transaction, string generation, int minor, string family,
        JsonElement entity, Dictionary<Guid, SnapshotMainContactReplacement> contactReplacements)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.Parameters.AddWithValue("$g", generation);
        void Add(string name, object? value) => insert.Parameters.AddWithValue(name, value ?? DBNull.Value);
        static string? Number(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);
        switch (family)
        {
            case "locations":
                SnapshotLocation location = Entity<SnapshotLocation>(entity);
                if (location.MainContactReplacement is { } replacement)
                {
                    if (!entity.TryGetProperty("mainContactReplacement", out JsonElement replacementJson)
                        || replacementJson.ValueKind != JsonValueKind.Object
                        || !replacementJson.TryGetProperty("isInactive", out JsonElement status)
                        || status.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                        || location.MainContact is not null || replacement.ContactId == Guid.Empty || string.IsNullOrWhiteSpace(replacement.Name))
                        throw new InvalidDataException("A main-contact replacement must identify an outgoing contact and no active Main.");
                    if (!contactReplacements.TryAdd(location.Id, replacement))
                        throw new InvalidDataException("A main-contact replacement Location is repeated.");
                }
                insert.CommandText = """
                    INSERT INTO locations(generation, id, name, customer_id, customer_name, town_id, town_name, county_name, eircode, main_contact)
                    VALUES($g, $id, $name, $customer, $customerName, $town, $townName, $county, $eircode, $contact)
                    """;
                Add("$id", location.Id.ToString()); Add("$name", location.Name); Add("$customer", location.CustomerId.ToString());
                Add("$customerName", location.CustomerName); Add("$town", location.Town.Id.ToString()); Add("$townName", location.Town.Name);
                Add("$county", location.Town.CountyName); Add("$eircode", location.Eircode);
                Add("$contact", location.MainContact is null ? null : JsonSerializer.Serialize(location.MainContact, SnapshotContract.JsonOptions));
                break;
            case "categories":
                SnapshotCategory category = Entity<SnapshotCategory>(entity);
                insert.CommandText = "INSERT INTO categories(generation, id, parent_id, name) VALUES($g, $id, $parent, $name)";
                Add("$id", category.Id.ToString()); Add("$parent", category.ParentId?.ToString()); Add("$name", category.Name);
                break;
            case "products":
                SnapshotProduct product = Entity<SnapshotProduct>(entity);
                insert.CommandText = """
                    INSERT INTO products(generation, id, code, name, category_id, unit, quantity_step, minimum_quantity, parent_product_id)
                    VALUES($g, $id, $code, $name, $category, $unit, $step, $minimum, $parent)
                    """;
                Add("$id", product.Id.ToString()); Add("$code", product.Code); Add("$name", product.Name);
                Add("$category", product.CategoryId.ToString()); Add("$unit", product.Unit); Add("$step", Number(product.QuantityStep));
                Add("$minimum", Number(product.MinimumQuantity)); Add("$parent", product.ParentProductId?.ToString());
                break;
            case "productAttributes":
                if (new[] { "productId", "position", "name", "value", "isArchived" }.Any(field => !entity.TryGetProperty(field, out _)))
                    throw new InvalidDataException("An attribute row is missing a required field.");
                SnapshotProductAttribute attribute = Entity<SnapshotProductAttribute>(entity);
                if (string.IsNullOrWhiteSpace(attribute.Name) || attribute.Value is null || attribute.Position < 0)
                    throw new InvalidDataException("An attribute row is invalid.");
                insert.CommandText = """
                    INSERT INTO product_attributes(generation, product_id, position, name, value, is_archived)
                    VALUES($g, $product, $position, $name, $value, $archived)
                    """;
                Add("$product", attribute.ProductId.ToString()); Add("$position", attribute.Position);
                Add("$name", attribute.Name); Add("$value", attribute.Value); Add("$archived", attribute.IsArchived ? 1 : 0);
                break;
            case "basePrices":
                SnapshotBasePrice price = Entity<SnapshotBasePrice>(entity);
                insert.CommandText = "INSERT INTO base_prices(generation, product_id, effective_from, amount) VALUES($g, $product, $from, $amount)";
                Add("$product", price.ProductId.ToString()); Add("$from", price.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture));
                Add("$amount", Number(price.Amount));
                break;
            case "lastStockCheckLines" or "recentOrderLines":
                if (!entity.TryGetProperty("locationId", out _) || !entity.TryGetProperty("productId", out _))
                    throw new InvalidDataException("A stock history row is missing a required field.");
                SnapshotStockCheckLine line = Entity<SnapshotStockCheckLine>(entity);
                insert.CommandText = $"""
                    INSERT INTO {(family == "lastStockCheckLines" ? "last_stock_check_lines" : "recent_order_lines")}(generation, location_id, product_id)
                    VALUES($g, $location, $product)
                    """;
                Add("$location", line.LocationId.ToString()); Add("$product", line.ProductId.ToString());
                try { insert.ExecuteNonQuery(); }
                catch (SqliteException repeated) when (repeated.SqliteErrorCode == 19)
                {
                    throw new InvalidDataException("A stock history row is repeated.", repeated);
                }
                if (family == "recentOrderLines") InsertQuantities(connection, transaction, generation, minor, entity, line);
                return;
            case "lastCallLowLines":
                if (new[] { "locationId", "callId", "calledOn", "productId" }.Any(field => !entity.TryGetProperty(field, out _)))
                    throw new InvalidDataException("A last Call gap row is missing a required field.");
                SnapshotLastCallLowLine gap = Entity<SnapshotLastCallLowLine>(entity);
                if (gap.CallId == Guid.Empty) throw new InvalidDataException("A last Call gap row is invalid.");
                insert.CommandText = """
                    INSERT INTO last_call_low_lines(generation, location_id, call_id, called_on, product_id)
                    VALUES($g, $location, $call, $on, $product)
                    """;
                Add("$location", gap.LocationId.ToString()); Add("$call", gap.CallId.ToString());
                Add("$on", gap.CalledOn.ToString("O", CultureInfo.InvariantCulture)); Add("$product", gap.ProductId.ToString());
                try { insert.ExecuteNonQuery(); }
                catch (SqliteException repeated) when (repeated.SqliteErrorCode == 19)
                {
                    throw new InvalidDataException("A last Call gap row is repeated.", repeated);
                }
                return;
            case "priceTiers":
                SnapshotPriceTier tier = Entity<SnapshotPriceTier>(entity);
                bool percentage = tier.Shape == SnapshotTierShapes.Percentage;
                if (tier.Id == Guid.Empty || string.IsNullOrWhiteSpace(tier.Name)
                    || !(percentage ? tier.PercentOff is > 0m and < 100m : tier.Shape == SnapshotTierShapes.PriceList && tier.PercentOff is null))
                    throw new InvalidDataException("A price tier row is invalid.");
                insert.CommandText = "INSERT INTO price_tiers(generation, id, name, shape, percent_off) VALUES($g, $id, $name, $shape, $percent)";
                Add("$id", tier.Id.ToString()); Add("$name", tier.Name); Add("$shape", tier.Shape); Add("$percent", Number(tier.PercentOff));
                break;
            case "priceTierTerms":
                SnapshotPriceTierTerm term = Entity<SnapshotPriceTierTerm>(entity);
                if ((term.PercentOff is null) == (term.FixedPrice is null) || term.PercentOff is <= 0m or >= 100m || term.FixedPrice is <= 0m)
                    throw new InvalidDataException("A tier term row is invalid.");
                insert.CommandText = """
                    INSERT INTO price_tier_terms(generation, tier_id, product_id, percent_off, fixed_price)
                    VALUES($g, $tier, $product, $percent, $fixed)
                    """;
                Add("$tier", term.TierId.ToString()); Add("$product", term.ProductId.ToString());
                Add("$percent", Number(term.PercentOff)); Add("$fixed", Number(term.FixedPrice));
                break;
            case "customerTiers":
                SnapshotCustomerTier held = Entity<SnapshotCustomerTier>(entity);
                insert.CommandText = "INSERT INTO customer_tiers(generation, customer_id, tier_id) VALUES($g, $customer, $tier)";
                Add("$customer", held.CustomerId.ToString()); Add("$tier", held.TierId.ToString());
                break;
            case "quantityBreaks":
                SnapshotQuantityBreak quantityBreak = Entity<SnapshotQuantityBreak>(entity);
                if (quantityBreak.FromQuantity <= 0m || quantityBreak.Price <= 0m) throw new InvalidDataException("A quantity break row is invalid.");
                insert.CommandText = "INSERT INTO quantity_breaks(generation, product_id, from_quantity, price) VALUES($g, $product, $from, $price)";
                Add("$product", quantityBreak.ProductId.ToString());
                Add("$from", quantityBreak.FromQuantity.ToString("0.############", CultureInfo.InvariantCulture));
                Add("$price", Number(quantityBreak.Price));
                break;
            default: throw new InvalidDataException("Unknown snapshot family.");
        }
        insert.ExecuteNonQuery();
    }

    // 1.5 (T-5.1.6): a recent order product's quantities, newest first. Required from 1.5 on, refused before it.
    private static void InsertQuantities(SqliteConnection connection, SqliteTransaction transaction, string generation, int minor,
        JsonElement entity, SnapshotStockCheckLine line)
    {
        bool present = entity.TryGetProperty("quantities", out JsonElement json) && json.ValueKind != JsonValueKind.Null;
        if (present != (minor >= 5))
            throw new InvalidDataException(minor >= 5 ? "A recent order row is missing its quantities." : "A recent order row has quantities this version doesn't carry.");
        if (!present) return;
        IReadOnlyList<SnapshotOrderedQuantity> quantities = Entity<SnapshotRecentOrderLine>(entity).Quantities ?? [];
        if (quantities.Count == 0 || quantities.Any(each => each is null || each.Quantity <= 0m || string.IsNullOrWhiteSpace(each.Unit)))
            throw new InvalidDataException("A recent order row's quantities are invalid.");
        for (int position = 0; position < quantities.Count; position++)
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO recent_order_quantities(generation, location_id, product_id, position, quantity, unit)
                VALUES($g, $location, $product, $position, $quantity, $unit)
                """;
            insert.Parameters.AddWithValue("$g", generation);
            insert.Parameters.AddWithValue("$location", line.LocationId.ToString());
            insert.Parameters.AddWithValue("$product", line.ProductId.ToString());
            insert.Parameters.AddWithValue("$position", position);
            insert.Parameters.AddWithValue("$quantity", quantities[position].Quantity.ToString(CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("$unit", quantities[position].Unit);
            insert.ExecuteNonQuery();
        }
    }

    private static T Entity<T>(JsonElement entity) =>
        entity.Deserialize<T>(SnapshotContract.JsonOptions) ?? throw new InvalidDataException("A snapshot entry is empty.");

    /// <summary>Deletes retired generations in bounded batches, so no single step holds the database for long.</summary>
    public async Task PurgeRetiredAsync(SnapshotHooks? hooks = null, CancellationToken ct = default)
    {
        hooks ??= new();
        bool reported = false;
        while (true)
        {
            await Gate.WaitAsync(ct);
            bool more;
            try { more = await Task.Run(PurgeBatch, ct); }
            finally { Gate.Release(); }
            if (!more) break;
            if (!reported) { reported = true; hooks.AtBoundary?.Invoke("purge-batch"); }
        }
        using SqliteConnection connection = Open();
        SqliteEngine.Execute(connection, "PRAGMA wal_checkpoint(PASSIVE)");
    }

    // One batch from one retired generation; the generation row goes once its rows are gone.
    private bool PurgeBatch()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand next = connection.CreateCommand();
        next.CommandText = "SELECT id FROM generations WHERE state='retired' ORDER BY id LIMIT 1";
        if (next.ExecuteScalar() is not string generation) return false;
        using SqliteTransaction transaction = connection.BeginTransaction();
        foreach (string table in ChildTables)
        {
            using SqliteCommand delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = $"DELETE FROM {table} WHERE rowid IN (SELECT rowid FROM {table} WHERE generation=$g LIMIT {PurgeBatchRows})";
            delete.Parameters.AddWithValue("$g", generation);
            if (delete.ExecuteNonQuery() > 0) { transaction.Commit(); return true; }
        }
        using SqliteCommand remove = connection.CreateCommand();
        remove.Transaction = transaction;
        remove.CommandText = "DELETE FROM generations WHERE id=$g AND state='retired'";
        remove.Parameters.AddWithValue("$g", generation);
        remove.ExecuteNonQuery();
        transaction.Commit();
        return true;
    }
}
