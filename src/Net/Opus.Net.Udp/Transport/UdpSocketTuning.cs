using System;
using System.Net.Sockets;

namespace Opus.Net.Udp.Transport;

/// <summary>
/// Shared low-level UDP socket tweaks that both the client and server transports need to
/// keep a Windows worker thread alive across the platform's UDP quirks.
/// </summary>
/// <remarks>
/// <para>
/// The classic gotcha: on Windows a UDP send to a port no peer is listening on returns
/// success at the syscall, but the kernel surfaces the ICMP Port Unreachable on the
/// *sender's* next <c>recv</c> as <c>WSAECONNRESET</c> / <see cref="SocketError.ConnectionReset"/>.
/// Untreated, this kills the receive worker the moment a heartbeat lands on a vanished
/// peer — exactly the symptom timing-out tests need to exercise. The fix is a one-shot
/// IOCTL (<see cref="SioUdpConnectionReset"/>) that disables the surfacing for the socket,
/// applied at bind / open time; combined with the <see cref="IsTransientReceiveError"/>
/// filter on every <c>ReceiveFrom</c> catch, every reasonable surviving error is treated
/// as "keep looping" rather than "give up".
/// </para>
/// </remarks>
internal static class UdpSocketTuning
{
    /// <summary>Windows IOCTL code for <c>SIO_UDP_CONNRESET</c> (controls whether ICMP
    /// port-unreachable replies surface as <c>WSAECONNRESET</c> on the next recv). Value
    /// taken from the Mswsock.h header; the sign comes from interpreting the unsigned
    /// constant <c>0x9800000C</c> as a 32-bit signed int.</summary>
    private const int SioUdpConnectionReset = -1744830452;

    /// <summary>On Windows, disables the surfacing of ICMP port-unreachable as a
    /// <see cref="SocketError.ConnectionReset"/> error on the next <c>recv</c>. No-op on
    /// non-Windows OSes (where the behaviour doesn't exist) and on socket implementations
    /// that don't recognise the ioctl (newer .NET runtimes might already disable it).</summary>
    public static void SuppressIcmpPortUnreachable(Socket socket)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            socket.IOControl(SioUdpConnectionReset, new byte[] { 0, 0, 0, 0 }, null);
        }
        catch (SocketException)
        {
            // Older OS without the ioctl — fall back on IsTransientReceiveError.
        }
    }

    /// <summary>Returns true for socket errors that should NOT kill the receive worker —
    /// errors that surface transiently and don't reflect a broken local socket.</summary>
    public static bool IsTransientReceiveError(SocketException ex) =>
        ex.SocketErrorCode is
            SocketError.ConnectionReset or
            SocketError.NetworkReset or
            SocketError.MessageSize or
            SocketError.HostUnreachable or
            SocketError.NetworkUnreachable;
}
