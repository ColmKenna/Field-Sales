using System.Text.Json;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>Streams a contract v1 snapshot one entity at a time, from the spike's verified bounded parser.</summary>
/// <remarks>
/// The only growable buffer is capped (1 MiB by default); a JsonDocument holds one entity, never a family.
/// The manifest must come first, so a foreign or unsupported snapshot is refused before any row is written.
/// A field this app does not know (a later minor version) is skipped value by value, never buffered whole.
/// </remarks>
internal sealed class SnapshotDocumentReader(Stream input, int maximumBufferBytes = 1024 * 1024)
{
    internal static readonly string[] Families = ["locations", "categories", "products", "basePrices"];
    private byte[] buffer = new byte[Math.Min(64 * 1024, maximumBufferBytes)];
    private int start, length;
    private bool final;
    private JsonReaderState state;

    public void Read(Action<SnapshotManifest> manifest, Action<string, JsonElement> row, CancellationToken ct)
    {
        RequireToken(JsonTokenType.StartObject, ct);
        HashSet<string> seen = new(StringComparer.Ordinal);
        bool hasManifest = false;
        while (true)
        {
            var (token, name) = Token(ct);
            if (token == JsonTokenType.EndObject) break;
            if (token != JsonTokenType.PropertyName || name is null || !seen.Add(name))
                throw new InvalidDataException("The snapshot has an invalid or repeated field.");
            if (name == "manifest")
            {
                using JsonDocument value = ValueOrArrayEnd(ct) ?? throw new InvalidDataException("The snapshot manifest is missing.");
                manifest(value.RootElement.Deserialize<SnapshotManifest>(SnapshotContract.JsonOptions)
                    ?? throw new InvalidDataException("The snapshot manifest is empty."));
                hasManifest = true;
            }
            else if (Families.Contains(name))
            {
                if (!hasManifest) throw new InvalidDataException("The snapshot manifest must come first.");
                RequireToken(JsonTokenType.StartArray, ct);
                while (ValueOrArrayEnd(ct) is { } entity)
                    using (entity)
                    {
                        if (entity.RootElement.ValueKind != JsonValueKind.Object)
                            throw new InvalidDataException("A snapshot entry must be an object.");
                        row(name, entity.RootElement);
                    }
            }
            else Skip(ct);
        }
        if (!hasManifest || Families.Any(family => !seen.Contains(family)))
            throw new InvalidDataException("The snapshot is missing a required field.");
        // Read to the end so the plain hash covers every byte.
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var reader = new Utf8JsonReader(buffer.AsSpan(start, length - start), final, state);
            if (reader.Read()) throw new InvalidDataException("The snapshot has trailing content.");
            Commit(ref reader);
            if (final) return;
            Fill(ct);
        }
    }

    // An additive field from a newer minor version: arrays are skipped one element at a time.
    private void Skip(CancellationToken ct)
    {
        if (Peek(ct) == JsonTokenType.StartArray)
        {
            RequireToken(JsonTokenType.StartArray, ct);
            while (ValueOrArrayEnd(ct) is { } element) element.Dispose();
        }
        else (ValueOrArrayEnd(ct) ?? throw new InvalidDataException("A snapshot field has no value.")).Dispose();
    }

    private void RequireToken(JsonTokenType expected, CancellationToken ct)
    {
        if (Token(ct).Token != expected) throw new InvalidDataException($"The snapshot is malformed: expected {expected}.");
    }

    private JsonTokenType Peek(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var reader = new Utf8JsonReader(buffer.AsSpan(start, length - start), final, state);
            if (reader.Read()) return reader.TokenType;
            if (final) throw new InvalidDataException("The snapshot is truncated.");
            Fill(ct);
        }
    }

    private (JsonTokenType Token, string? Name) Token(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var reader = new Utf8JsonReader(buffer.AsSpan(start, length - start), final, state);
            if (reader.Read())
            {
                var result = (reader.TokenType, reader.TokenType == JsonTokenType.PropertyName ? reader.GetString() : null);
                Commit(ref reader);
                return result;
            }
            Commit(ref reader);
            if (final) throw new InvalidDataException("The snapshot is truncated.");
            Fill(ct);
        }
    }

    private JsonDocument? ValueOrArrayEnd(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var reader = new Utf8JsonReader(buffer.AsSpan(start, length - start), final, state);
            if (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) { Commit(ref reader); return null; }
                if (JsonDocument.TryParseValue(ref reader, out JsonDocument? document))
                {
                    Commit(ref reader);
                    return document;
                }
            }
            if (final) throw new InvalidDataException("The snapshot is truncated.");
            // Reparse only this incomplete value, from the reader state before its first token.
            Fill(ct);
        }
    }

    private void Commit(ref Utf8JsonReader reader)
    {
        start += checked((int)reader.BytesConsumed);
        state = reader.CurrentState;
    }

    private void Fill(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int remaining = length - start;
        buffer.AsSpan(start, remaining).CopyTo(buffer);
        start = 0;
        length = remaining;
        if (remaining == buffer.Length)
        {
            if (buffer.Length >= maximumBufferBytes) throw new InvalidDataException("A snapshot entry is too large.");
            Array.Resize(ref buffer, Math.Min(maximumBufferBytes, checked(buffer.Length * 2)));
        }
        int read = input.Read(buffer, length, buffer.Length - length);
        length += read;
        final = read == 0;
    }
}
