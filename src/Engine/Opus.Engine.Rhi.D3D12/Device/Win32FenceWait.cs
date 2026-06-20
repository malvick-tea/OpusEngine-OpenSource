using System;
using System.Runtime.InteropServices;
using Opus.Foundation;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using UnmanagedType = System.Runtime.InteropServices.UnmanagedType;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Fence-based GPU drain implementation. Owns the
/// per-device <see cref="ID3D12Fence"/> + Win32 event handle pair used by
/// <c>WaitForIdle</c>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "MA0055:Do not use finalizer",
    Justification = "The finalizer releases only an unmanaged fence and event handle.")]
internal sealed unsafe class Win32FenceWait : IDisposable
{
    private const uint WaitFiveSeconds = 5000u;

    private readonly ID3D12Device* _device;
    private ID3D12Fence* _fence;
    private nint _waitEvent;
    private ulong _nextSignal = 1;

    ~Win32FenceWait()
    {
        ReleaseNative();
    }

    public Win32FenceWait(ID3D12Device* device)
    {
        _device = device;
    }

    /// <summary>Signals the fence on <paramref name="queue"/> then blocks the calling thread
    /// until the GPU reaches that signal. Allocates the fence + event lazily on first use.</summary>
    public void Drain(ID3D12CommandQueue* queue)
    {
        EnsureFence();

        var value = _nextSignal++;
        SilkMarshal.ThrowHResult(queue->Signal(_fence, value));

        if (_fence->GetCompletedValue() < value)
        {
            SilkMarshal.ThrowHResult(_fence->SetEventOnCompletion(value, (void*)_waitEvent));
            // Bounded wait — see D3D12SwapChain.Present for the same rationale. A TDR'd GPU
            // surfaces as a managed exception so the SDL message pump keeps running and the
            // OS doesn't mark the testhost as hung.
            var waitResult = WaitForSingleObject(_waitEvent, WaitFiveSeconds);
            if (waitResult != 0u)
            {
                var removed = _device->GetDeviceRemovedReason();
                throw new EngineDeviceLostException(
                    $"D3D12 idle-fence wait timed out after 5s (signal={value}, completed={_fence->GetCompletedValue()}, WaitForSingleObject={waitResult:X8}, deviceRemovedReason=0x{removed:X8}). GPU is likely in TDR.",
                    removed);
            }
        }
    }

    private void EnsureFence()
    {
        if (_fence != null)
        {
            return;
        }

        var fenceGuid = ID3D12Fence.Guid;
        ID3D12Fence* fence = null;
        SilkMarshal.ThrowHResult(_device->CreateFence(0, FenceFlags.None, &fenceGuid, (void**)&fence));
        _fence = fence;

        _waitEvent = CreateEventW(IntPtr.Zero, false, false, null);
        if (_waitEvent == 0)
        {
            throw new InvalidOperationException("CreateEventW returned a null event handle for idle wait.");
        }
    }

    public void Dispose()
    {
        ReleaseNative();
        GC.SuppressFinalize(this);
    }

    private void ReleaseNative()
    {
        if (_fence != null)
        {
            _fence->Release();
            _fence = null;
        }

        if (_waitEvent != 0)
        {
            CloseHandle(_waitEvent);
            _waitEvent = 0;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateEventW(IntPtr lpEventAttributes, [MarshalAs(UnmanagedType.Bool)] bool bManualReset, [MarshalAs(UnmanagedType.Bool)] bool bInitialState, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);
}
