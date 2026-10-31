using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet.Core;

/// <summary>T-01 (T1.2): turns a technical upload fault into a sentence for the rep. The original
/// code and server detail are kept separately for support, never interpolated into this sentence.</summary>
public static class UploadFaultMessages
{
    public const string Incomplete = "Couldn't be sent - the upload was incomplete";
    public const string ContactOffice = "This couldn't be sent - contact the office";

    /// <summary>Only an incomplete upload has a more specific agreed sentence. Corrupt, unsupported,
    /// not-yours, id-in-use and too-large need office help; an unrecognised or absent code does too.
    /// AlreadyReceived is a successful receipt, not a fault to pass to this mapping.</summary>
    public static string For(string? fault) => fault == UploadFaults.Incomplete ? Incomplete : ContactOffice;
}
