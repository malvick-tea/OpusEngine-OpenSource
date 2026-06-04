using System;

namespace Opus.Engine.Net.Soak;

/// <summary>
/// One issue surfaced by a <see cref="NetSoakHarness"/> run. Carries the stable code, a
/// human-readable detail, the optional peer index involved, and the observation
/// timestamp.
/// </summary>
public sealed record NetSoakIssue(
    NetSoakIssueCode Code,
    int PeerIndex,
    string Detail,
    DateTimeOffset CapturedAtUtc)
{
    /// <summary>Sentinel value for issues that are not tied to a specific peer.</summary>
    public const int NoPeerIndex = -1;

    /// <summary>The stable <c>OPDX-NET-*</c> diagnostic code for this issue.</summary>
    public string DiagnosticCode => Code switch
    {
        NetSoakIssueCode.PeerUnconnected => NetDiagnosticCodes.SoakPeerUnconnected,
        NetSoakIssueCode.PayloadCorruption => NetDiagnosticCodes.SoakPayloadCorruption,
        NetSoakIssueCode.PacketDropped => NetDiagnosticCodes.SoakPacketDropped,
        NetSoakIssueCode.BudgetExceeded => NetDiagnosticCodes.SoakBudgetExceeded,
        NetSoakIssueCode.TransportFault => NetDiagnosticCodes.SoakTransportFault,
        _ => "OPDX-NET-???",
    };

    /// <summary>Creates an issue attached to a specific peer index.</summary>
    public static NetSoakIssue ForPeer(
        NetSoakIssueCode code,
        int peerIndex,
        string detail,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentOutOfRangeException.ThrowIfNegative(peerIndex);
        return new NetSoakIssue(code, peerIndex, detail, capturedAtUtc.ToUniversalTime());
    }

    /// <summary>Creates an issue that is not tied to a specific peer.</summary>
    public static NetSoakIssue Global(NetSoakIssueCode code, string detail, DateTimeOffset capturedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return new NetSoakIssue(code, NoPeerIndex, detail, capturedAtUtc.ToUniversalTime());
    }
}
