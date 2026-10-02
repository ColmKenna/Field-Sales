using System.Text;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Directory;

public sealed record GeographySeedRow(string Region, string County, string Town);

public static class GeographyCsv
{
    public static IReadOnlyList<GeographySeedRow> Parse(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > GeographyImportLimits.MaximumBytes)
            throw new GeographyValidationException("The CSV file must be 1 MiB or smaller.");
        List<GeographySeedRow> result = [];
        bool headerSeen = false;
        int row = 0;
        foreach (string[] fields in Records(text.TrimStart('\uFEFF')))
        {
            row++;
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            if (!headerSeen)
            {
                if (!fields.SequenceEqual(new[] { "Region", "County", "Town" }))
                    throw new GeographyValidationException("The CSV header must be Region,County,Town.");
                headerSeen = true;
                continue;
            }
            if (result.Count >= GeographyImportLimits.MaximumRows)
                throw new GeographyValidationException("The CSV file must contain at most 10,000 town records.");
            if (fields.Length != 3)
                throw new GeographyValidationException($"Row {row}: enter exactly Region, County and Town.");
            try
            {
                result.Add(new(GeographyNames.Validate(fields[0]), GeographyNames.Validate(fields[1]), GeographyNames.Validate(fields[2])));
            }
            catch (GeographyValidationException exception)
            {
                throw new GeographyValidationException($"Row {row}: {exception.Message}");
            }
        }
        if (result.Count == 0) throw new GeographyValidationException("The CSV file contains no towns.");
        return result;
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
