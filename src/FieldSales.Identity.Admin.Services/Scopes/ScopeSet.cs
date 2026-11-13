using System.Collections;

namespace FieldSales.Identity.Services.Scopes;

/// <summary>
///     A distinct, ordered collection of scope names used at service boundaries.
/// </summary>
public sealed class ScopeSet : IReadOnlyCollection<ScopeName>
{
    private readonly ScopeName[] scopes;

    public ScopeSet(IEnumerable<ScopeName> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        this.scopes = scopes
            .Where(scope => !scope.IsEmpty)
            .Distinct()
            .OrderBy(scope => scope)
            .ToArray();
    }

    public static ScopeSet Empty { get; } = new(Array.Empty<ScopeName>());

    public int Count => scopes.Length;

    public IEnumerator<ScopeName> GetEnumerator() => ((IEnumerable<ScopeName>)scopes).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Contains(ScopeName scope) => Array.BinarySearch(scopes, scope) >= 0;

    public IReadOnlyList<string> ToValues() => scopes.Select(scope => scope.Value).ToArray();

    public static ScopeSet FromStrings(IEnumerable<string>? scopes) =>
        scopes is null ? Empty : new ScopeSet(scopes.Select(ScopeName.Create));
}