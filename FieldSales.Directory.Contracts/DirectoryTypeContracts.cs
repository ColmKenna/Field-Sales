using FieldSales.ReferenceData;

namespace FieldSales.Directory.Contracts;

public sealed record DirectoryTypeChoice(Guid Id, string Name, string? Description, bool IsArchived = false)
{
    public string Label => Name + (IsArchived ? " (archived)" : "");
}
public sealed record SaveDirectoryTypeRequest(string? Name, string? Description = null, string? Version = null);
public sealed record RetireDirectoryTypeRequest(ReferenceAction Action, string? Version);
public sealed record DirectoryTypeMutationResult(bool Saved);
