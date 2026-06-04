using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Captured D3D12 debug-layer message. Severity + ID + description suffice for the
/// 90% of cases — full <c>D3D12_MESSAGE</c> includes a stack of context strings that
/// Silk.NET doesn't surface through the callback signature.
/// </summary>
public readonly record struct D3D12DebugMessage(
    MessageCategory Category,
    MessageSeverity Severity,
    MessageID Id,
    string Description)
{
    public override string ToString() => $"[{Severity}/{Category}/#{(int)Id}] {Description}";
}

/// <summary>
/// Hooks <c>ID3D12InfoQueue1.RegisterMessageCallback</c> so D3D12 validation messages
/// are captured synchronously inside the call that produced them — which means we
/// see *why* a bad <c>CreateGraphicsPipelineState</c> faulted before the native AV
/// terminates the process.
///
/// Only available on Windows 10 1903+ (where ID3D12InfoQueue1 ships). On older
/// hosts <see cref="TryAttach"/> returns null and the device runs without diagnostic
/// capture — debug layer messages still print to the VS Output window via the
/// regular DXGI debug pipeline, just without programmatic access from C#.
///
/// Messages are mirrored to:
/// <list type="bullet">
/// <item><description>An in-memory queue (read via <see cref="Snapshot"/>);</description></item>
/// <item><description><see cref="System.Diagnostics.Debug.WriteLine"/> for VS Output / DbgView;</description></item>
/// <item><description>An optional log file flushed after every message — survives a native
///     AV that takes down the test host before in-memory state can be dumped.</description></item>
/// </list>
/// </summary>
public sealed unsafe class D3D12DebugMessenger : IDisposable
{
    private static readonly object FileLogGuard = new();
    private static StreamWriter? _fileLog;
    private static string? _fileLogPath;

    private ID3D12InfoQueue1* _infoQueue1;
    private uint _callbackCookie;
    private GCHandle _contextHandle;
    private readonly ConcurrentQueue<D3D12DebugMessage> _messages = new();
    private bool _disposed;

    private D3D12DebugMessenger(ID3D12InfoQueue1* infoQueue1, uint cookie, GCHandle contextHandle)
    {
        _infoQueue1 = infoQueue1;
        _callbackCookie = cookie;
        _contextHandle = contextHandle;
    }

    /// <summary>True if the most recent capture window contains any Error / Corruption messages.</summary>
    public bool HasErrors => _messages.Any(m =>
        m.Severity == MessageSeverity.Error || m.Severity == MessageSeverity.Corruption);

    /// <summary>Thread-safe snapshot of every message captured so far. Allocates a new array on each call.</summary>
    public IReadOnlyList<D3D12DebugMessage> Snapshot() => _messages.ToArray();

    /// <summary>Drains the message queue so the next call window starts clean.</summary>
    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
            // Discard.
        }
    }

    /// <summary>Opens (or rolls) a file at <paramref name="path"/> that every captured
    /// message is appended to with an immediate flush — survives a native AV that
    /// terminates the process before in-memory state can be dumped. Pass null to
    /// disable. Static because the callback can't carry per-instance state cheaply.</summary>
    public static void SetLogFile(string? path)
    {
        lock (FileLogGuard)
        {
            _fileLog?.Dispose();
            _fileLog = null;
            _fileLogPath = path;

            if (!string.IsNullOrEmpty(path))
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                _fileLog = new StreamWriter(path, append: false) { AutoFlush = true };
                _fileLog.WriteLine($"[D3D12] log opened at {DateTime.Now:O}");
            }
        }
    }

    public static string? CurrentLogFilePath => _fileLogPath;

    /// <summary>
    /// Tries to attach to <paramref name="device"/>. Returns null when ID3D12InfoQueue1
    /// isn't queryable (no debug layer enabled, or Windows pre-1903) — caller decides
    /// whether that's fatal.
    /// </summary>
    public static D3D12DebugMessenger? TryAttach(ID3D12Device* device)
    {
        if (device == null)
        {
            return null;
        }

        var iqGuid = ID3D12InfoQueue1.Guid;
        ID3D12InfoQueue1* iq1 = null;
        var hr = ((IUnknown*)device)->QueryInterface(&iqGuid, (void**)&iq1);
        if (hr < 0 || iq1 == null)
        {
            return null;
        }

        // Allocate a state object that the callback dereferences via GCHandle.
        var state = new MessengerState();
        var contextHandle = GCHandle.Alloc(state);

        try
        {
            uint cookie = 0;
            var pfn = new PfnMessageFunc(&OnDebugMessage);

            hr = iq1->RegisterMessageCallback(
                pfn,
                MessageCallbackFlags.FlagNone,
                (void*)GCHandle.ToIntPtr(contextHandle),
                &cookie);

            if (hr < 0)
            {
                iq1->Release();
                contextHandle.Free();
                return null;
            }

            var messenger = new D3D12DebugMessenger(iq1, cookie, contextHandle);
            state.Owner = messenger;
            return messenger;
        }
        catch
        {
            iq1->Release();
            if (contextHandle.IsAllocated)
            {
                contextHandle.Free();
            }

            throw;
        }
    }

    /// <summary>
    /// Native callback fired by the D3D12 runtime. Stays minimal — no managed allocations
    /// on the hot path beyond marshalling the description string. Forwards each message
    /// both to the in-memory queue and to <c>Debug.WriteLine</c> so a debugger / DbgView
    /// sees them live.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnDebugMessage(
        MessageCategory category,
        MessageSeverity severity,
        MessageID id,
        byte* pDescription,
        void* pContext)
    {
        if (pContext == null)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)pContext);
        if (!handle.IsAllocated || handle.Target is not MessengerState state)
        {
            return;
        }

        var owner = state.Owner;
        if (owner is null || owner._disposed)
        {
            return;
        }

        var description = pDescription != null
            ? Marshal.PtrToStringAnsi((IntPtr)pDescription) ?? string.Empty
            : string.Empty;
        var message = new D3D12DebugMessage(category, severity, id, description);
        owner._messages.Enqueue(message);
        System.Diagnostics.Debug.WriteLine($"[D3D12] {message}");

        // File mirror — flushed immediately so a subsequent native AV doesn't take the
        // diagnostic with it. Lock guards against concurrent callback invocations on
        // worker threads.
        if (_fileLog != null)
        {
            lock (FileLogGuard)
            {
                _fileLog?.WriteLine($"[D3D12] {message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_infoQueue1 != null)
        {
            try
            {
                _infoQueue1->UnregisterMessageCallback(_callbackCookie);
            }
            catch
            {
                // Best-effort — we're tearing down.
            }

            _infoQueue1->Release();
            _infoQueue1 = null;
        }

        if (_contextHandle.IsAllocated)
        {
            _contextHandle.Free();
        }
    }

    /// <summary>Heap object the callback resolves through the GCHandle context pointer.
    /// Keeps the messenger reference behind one level of indirection so we can null
    /// the field on dispose without invalidating the GCHandle target.</summary>
    private sealed class MessengerState
    {
        public D3D12DebugMessenger? Owner;
    }
}
