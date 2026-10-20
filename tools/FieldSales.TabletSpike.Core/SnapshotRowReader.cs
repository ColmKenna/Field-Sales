using System.Text.Json;

namespace FieldSales.TabletSpike.Core;

// The only growable buffer is capped at 1 MiB. A JsonDocument owns one entity, never a family/snapshot.
public sealed class SnapshotRowReader(Stream input, int maximumBufferBytes = 1024 * 1024)
{
    private byte[] buffer = new byte[Math.Min(64 * 1024, maximumBufferBytes)];
    private int start, length;
    private bool final;
    private JsonReaderState state;
    public int MaxBufferBytes { get; private set; } = Math.Min(64 * 1024, maximumBufferBytes);

    public void Read(FixtureManifest manifest, Action<string, JsonElement> row, CancellationToken ct)
    {
        RequireToken(JsonTokenType.StartObject, ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var (token, name) = Token(ct);
            if (token == JsonTokenType.EndObject) break;
            if (token != JsonTokenType.PropertyName || name is null || !seen.Add(name))
                throw new InvalidDataException("Invalid/duplicate envelope property.");
            if (FixtureManifest.Families.Contains(name))
            {
                RequireToken(JsonTokenType.StartArray, ct);
                while (true)
                {
                    using var entity = ValueOrArrayEnd(ct);
                    if (entity is null) break;
                    if (entity.RootElement.ValueKind != JsonValueKind.Object)
                        throw new InvalidDataException("An entity must be an object.");
                    row(name, entity.RootElement);
                }
            }
            else
            {
                using var value = ValueOrArrayEnd(ct) ?? throw new InvalidDataException("Missing header value.");
                bool valid = name switch
                {
                    "schema" => value.RootElement.GetString() == FixtureManifest.Format,
                    "ownerSubject" => value.RootElement.GetString() == manifest.OwnerSubject,
                    "seed" => value.RootElement.GetInt32() == manifest.Seed,
                    "asOf" => value.RootElement.GetDateTime().Date == manifest.AsOf.ToDateTime(TimeOnly.MinValue),
                    _ => false
                };
                if (!valid) throw new InvalidDataException($"Invalid snapshot header: {name}.");
            }
        }
        if (seen.Count != FixtureManifest.Families.Length + 4 ||
            FixtureManifest.Families.Any(f => !seen.Contains(f)) ||
            new[] { "schema", "ownerSubject", "seed", "asOf" }.Any(f => !seen.Contains(f)))
            throw new InvalidDataException("Missing snapshot header/family.");
        // Consume trailing whitespace and reach EOF so the plain length/hash covers all bytes.
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var reader = new Utf8JsonReader(buffer.AsSpan(start, length - start), final, state);
            if (reader.Read()) throw new InvalidDataException("Trailing snapshot content.");
            Commit(ref reader);
            if (final) return;
            Fill(ct);
        }
    }

    private void RequireToken(JsonTokenType expected, CancellationToken ct)
    {
        if (Token(ct).Token != expected) throw new InvalidDataException($"Expected {expected}.");
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
            if (final) throw new InvalidDataException("Truncated snapshot.");
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
                if (JsonDocument.TryParseValue(ref reader, out var document))
                {
                    Commit(ref reader);
                    return document;
                }
            }
            if (final) throw new InvalidDataException("Truncated entity.");
            // Reparse only this incomplete entity, with the reader state from before its first token.
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
            if (buffer.Length >= maximumBufferBytes) throw new InvalidDataException("Entity/token exceeds bounded parser capacity.");
            Array.Resize(ref buffer, Math.Min(maximumBufferBytes, checked(buffer.Length * 2)));
            MaxBufferBytes = buffer.Length;
        }
        int read = input.Read(buffer, length, buffer.Length - length);
        length += read;
        final = read == 0;
    }
}
