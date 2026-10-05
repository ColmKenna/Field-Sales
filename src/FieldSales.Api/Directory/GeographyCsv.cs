using System.Text;
using System.Globalization;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Directory;

public sealed record GeographySeedRow(string Region, string County, string Town, Coordinates? Coordinates = null);

public static class GeographyCsv
{
    public static IReadOnlyList<GeographySeedRow> Parse(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > GeographyImportLimits.MaximumBytes)
            throw new GeographyValidationException("The CSV file must be 1 MiB or smaller.");
        List<GeographySeedRow> result = [];
        bool headerSeen = false;
        int columns = 3;
        int row = 0;
        foreach (string[] fields in Records(text.TrimStart('\uFEFF')))
        {
            row++;
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            if (!headerSeen)
            {
                if (fields.SequenceEqual(new[] { "Region", "County", "Town", "Latitude", "Longitude" })) columns = 5;
                else if (!fields.SequenceEqual(new[] { "Region", "County", "Town" }))
                    throw new GeographyValidationException("The CSV header must be Region,County,Town or Region,County,Town,Latitude,Longitude.");
                headerSeen = true;
                continue;
            }
            if (result.Count >= GeographyImportLimits.MaximumRows)
                throw new GeographyValidationException("The CSV file must contain at most 10,000 town records.");
            if (fields.Length != columns)
                throw new GeographyValidationException($"Row {row}: enter exactly {columns} columns to match the header.");
            try
            {
                Coordinates? coordinates = columns == 5 ? Coordinates.FromPair(Number(fields[3], "latitude"), Number(fields[4], "longitude")) : null;
                result.Add(new(GeographyNames.Validate(fields[0]), GeographyNames.Validate(fields[1]), GeographyNames.Validate(fields[2]), coordinates));
            }
            catch (GeographyValidationException exception)
            {
                throw new GeographyValidationException($"Row {row}: {exception.Message}");
            }
            catch (CustomerDirectoryValidationException exception)
            { throw new GeographyValidationException($"Row {row}: {exception.Message}"); }
        }
        if (result.Count == 0) throw new GeographyValidationException("The CSV file contains no towns.");
        return result;
    }

    private static decimal? Number(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
            throw new GeographyValidationException($"Enter {label} as a decimal number with a dot, for example 52.92.");
        return number;
    }

    private static IEnumerable<string[]> Records(string text)
    {
        List<string> fields = [];
        StringBuilder field = new();
        bool quoted = false, closedQuote = false, started = false;
        int row = 1;
        for (int i = 0; i < text.Length; i++)
        {
            char value = text[i];
            started = true;
            if (quoted)
            {
                if (value == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else { quoted = false; closedQuote = true; }
                }
                else field.Append(value);
                continue;
            }
            if (value is ',' or '\r' or '\n')
            {
                fields.Add(field.ToString()); field.Clear(); closedQuote = false;
                if (value == ',') continue;
                if (value == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                yield return fields.ToArray(); fields.Clear(); started = false; row++;
            }
            else if (value == '"' && field.Length == 0 && !closedQuote) quoted = true;
            else if (value == '"' || closedQuote)
                throw new GeographyValidationException($"Row {row}: invalid CSV quoting.");
            else field.Append(value);
        }
        if (quoted) throw new GeographyValidationException($"Row {row}: close the quoted field.");
        if (started) { fields.Add(field.ToString()); yield return fields.ToArray(); }
    }
}
