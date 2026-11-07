using System.Globalization;
using FieldSales.Pricing;
using FieldSales.Snapshot.Contracts;
using Microsoft.Data.Sqlite;

namespace FieldSales.Tablet.Core;

public sealed partial class SnapshotStore
{
    /// <summary>What <paramref name="locationId"/> prices with on <paramref name="today"/> (WI-064 D2): its customer's tiers and every
    /// product's quantity breaks, read from one active generation, on the price day (WI-059 D2). A shop the snapshot doesn't carry,
    /// or whose customer holds no tier, prices at list price and breaks.</summary>
    public LocationPrices ReadLocationPrices(Guid locationId, DateOnly today)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        DateOnly day = PriceDay(command, today);
        command.CommandText = "SELECT generation FROM active";
        if (command.ExecuteScalar() is not string generation)
        {
            transaction.Commit();
            return new(day, [], new Dictionary<Guid, IReadOnlyList<QuantityBreak>>());
        }
        command.Parameters.AddWithValue("$g", generation);
        command.Parameters.AddWithValue("$location", locationId.ToString());

        // The customer's tiers and their terms, one row per term (or one with no term for a tier that has none).
        command.CommandText = """
            SELECT r.id, r.name, r.shape, r.percent_off, t.product_id, t.percent_off, t.fixed_price
            FROM locations l
            JOIN customer_tiers c ON c.generation=l.generation AND c.customer_id=l.customer_id
            JOIN price_tiers r ON r.generation=c.generation AND r.id=c.tier_id
            LEFT JOIN price_tier_terms t ON t.generation=r.generation AND t.tier_id=r.id
            WHERE l.generation=$g AND l.id=$location
            """;
        Dictionary<string, (string Name, string Shape, decimal? PercentOff, Dictionary<Guid, (decimal? PercentOff, decimal? FixedPrice)> Terms)> held = [];
        using (SqliteDataReader reader = command.ExecuteReader())
            while (reader.Read())
            {
                string id = reader.GetString(0);
                if (!held.TryGetValue(id, out var tier))
                    held[id] = tier = (reader.GetString(1), reader.GetString(2), Number(reader, 3), []);
                if (!reader.IsDBNull(4)) tier.Terms[Guid.Parse(reader.GetString(4))] = (Number(reader, 5), Number(reader, 6));
            }
        PriceTier[] tiers = [.. held.Values.Select(tier => tier.Shape == SnapshotTierShapes.PriceList
            ? (PriceTier)new PriceListTier(tier.Name, tier.Terms.Where(term => term.Value.FixedPrice is not null)
                .ToDictionary(term => term.Key, term => term.Value.FixedPrice!.Value))
            : new PercentageTier(tier.Name, tier.PercentOff ?? 0m, tier.Terms.ToDictionary(term => term.Key,
                term => new TierOverride(term.Value.PercentOff, term.Value.FixedPrice))))];

        command.CommandText = "SELECT product_id, from_quantity, price FROM quantity_breaks WHERE generation=$g";
        Dictionary<Guid, List<QuantityBreak>> breaks = [];
        using (SqliteDataReader reader = command.ExecuteReader())
            while (reader.Read())
            {
                Guid product = Guid.Parse(reader.GetString(0));
                if (!breaks.TryGetValue(product, out List<QuantityBreak>? list)) breaks[product] = list = [];
                list.Add(new(decimal.Parse(reader.GetString(1), CultureInfo.InvariantCulture), decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
            }
        transaction.Commit();
        return new(day, tiers, breaks.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<QuantityBreak>)entry.Value));
    }
}
