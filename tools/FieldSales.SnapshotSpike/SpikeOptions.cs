using System.Globalization;

namespace FieldSales.SnapshotSpike;

public sealed record SpikeOptions(string Profile, SpikeScale Scale, int Seed, DateOnly AsOf,
    string OutputDirectory, string UnsentPath)
{
    public string OwnerSubject => "spike-rep-" + Seed.ToString(CultureInfo.InvariantCulture);

    public static SpikeOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] allowed = ["--profile", "--products", "--locations", "--history-orders", "--history-lines",
            "--seed", "--as-of", "--output", "--unsent"];
        for (int i = 0; i < args.Length; i += 2)
        {
            if (!allowed.Contains(args[i]) || i + 1 == args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                || !values.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Use known, non-repeated options with a value after each option. See --help.");
        }
        string Required(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"{key} is required.");
        int Number(string key, int fallback) => !values.TryGetValue(key, out var value) ? fallback
            : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result
            : throw new ArgumentException($"{key} must be a whole number within the 32-bit range.");

        string profile = values.GetValueOrDefault("--profile", "baseline");
        var defaults = SpikeProfiles.Get(profile);
        var scale = new SpikeScale(Number("--products", defaults.Products), Number("--locations", defaults.Locations),
            Number("--history-orders", defaults.OrdersPerLocation), Number("--history-lines", defaults.LinesPerOrder));
        _ = scale.Counts(); // Reject derived-count overflow before creating any files.
        if (!DateOnly.TryParseExact(Required("--as-of"), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var asOf))
            throw new ArgumentException("--as-of must be a valid yyyy-MM-dd date.");
        _ = asOf.AddDays(365);
        _ = asOf.AddDays(checked(-30 * Math.Max(scale.OrdersPerLocation, 1)));
        int seed = Number("--seed", 42);
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Required("--output")));
        string parent = Path.GetDirectoryName(output)
            ?? throw new ArgumentException("Choose a new output directory below an existing root.");
        string unsent = Path.GetFullPath(values.TryGetValue("--unsent", out var explicitPath)
            ? explicitPath : Path.Combine(parent, $"unsent-{seed.ToString(CultureInfo.InvariantCulture)}.json"));
        var options = new SpikeOptions(profile, scale, seed, asOf, output, unsent);
        options.Validate();
        return options;
    }

    public void Validate()
    {
        _ = SpikeProfiles.Get(Profile);
        _ = Scale.Counts();
        _ = AsOf.AddDays(365);
        _ = AsOf.AddDays(checked(-30 * Math.Max(Scale.OrdersPerLocation, 1)));
        if (!Path.IsPathFullyQualified(OutputDirectory) || !Path.IsPathFullyQualified(UnsentPath)
            || string.IsNullOrEmpty(Path.GetDirectoryName(OutputDirectory)))
            throw new ArgumentException("Output and Unsent paths must resolve to full paths below a root.");
        if (Within(UnsentPath, OutputDirectory) || Within(OutputDirectory, UnsentPath))
            throw new ArgumentException("The Unsent file must be separate from the snapshot output directory.");
        if (Directory.Exists(OutputDirectory) || File.Exists(OutputDirectory))
            throw new ArgumentException("Output already exists. Choose a new directory; existing exports are never overwritten.");
        if (Directory.Exists(UnsentPath))
            throw new ArgumentException("The Unsent path must name a file, not a directory.");
    }

    private static bool Within(string candidate, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return candidate.Equals(root, comparison) || candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }
}
