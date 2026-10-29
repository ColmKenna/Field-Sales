using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

/// <summary>An immutable, rep-local projection of one active snapshot. No network or persisted search schema.</summary>
public sealed class LocationSearchIndex
{
    private readonly (HomeLocation Location, string Name)[] rows;
    public string? Generation { get; }
    public int Count => rows.Length;

    internal LocationSearchIndex(string? generation, IEnumerable<HomeLocation> locations)
    {
        Generation = generation;
        rows = locations.Select(location => (Location: location, Name: Normalize(location.Name)))
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ThenBy(row => Normalize(row.Location.TownName), StringComparer.Ordinal)
            .ThenBy(row => row.Location.Id).ToArray();
    }

    public List<HomeLocation> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        string name = Normalize(query.Trim());
        if (name.Length == 0) return [];
        return rows.Where(row => row.Name.Contains(name, StringComparison.Ordinal)).Select(row => row.Location).ToList();
    }

    public HomeLocation? Find(Guid id) => rows.FirstOrDefault(row => row.Location.Id == id).Location;

    internal static string Normalize(string value)
    {
        StringBuilder result = new();
        foreach (Rune rune in value.Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark) continue;
            result.Append(rune.Value is 0x2018 or 0x2019 ? "'" : Rune.ToUpperInvariant(rune).ToString());
        }
        return result.ToString();
    }
}

public sealed partial class SnapshotStore
{
    /// <summary>Read the generation and Locations in one transaction so a concurrent Sync cannot mix copies.</summary>
    public LocationSearchIndex ReadLocationIndex()
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT generation FROM active";
        string? generation = command.ExecuteScalar() as string;
        List<HomeLocation> locations = [];
        if (generation is not null)
        {
            command.CommandText = "SELECT id, name, town_name, customer_name FROM locations WHERE generation=$g";
            command.Parameters.AddWithValue("$g", generation);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read()) locations.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }
        transaction.Commit();
        return new(generation, locations);
    }
}
