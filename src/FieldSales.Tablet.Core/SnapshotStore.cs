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
    private const int SchemaVersion = 2;
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
    private static readonly string[] ChildTables = ["product_attributes", "base_prices", "products", "categories", "locations"];
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
        try { SqliteEngine.EnsureSchema(connection, SchemaVersion, Schema + AttributeSchema, AttributeSchema); }
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
        int rows = 0;
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
                        Validate(received);
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
                        Insert(connection, batch, generation, family, entity, contactReplacements);
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
            if (declared != new SnapshotCounts(counts["locations"], counts["categories"], counts["products"], counts["basePrices"], counts["productAttributes"]))
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

    private void Validate(SnapshotManifest manifest)
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
            || (minor == 0 && manifest.Counts.ProductAttributes != 0))
            throw new InvalidDataException("The snapshot manifest is invalid.");
    }

    private static void Insert(SqliteConnection connection, SqliteTransaction transaction, string generation, string family,
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
            default: throw new InvalidDataException("Unknown snapshot family.");
        }
        insert.ExecuteNonQuery();
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
