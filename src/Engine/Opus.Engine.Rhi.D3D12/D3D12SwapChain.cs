using System;
using System.Runtime.InteropServices;
using System.Threading;
using Opus.Foundation;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// DXGI swap chain attached to an OS window — owns the back buffers, the per-buffer
/// render target views, and the per-frame fence used to keep the CPU at most
/// <see cref="BufferCount"/>-1 frames ahead of the GPU.
///
/// Lifecycle:
/// <list type="number">
/// <item><description><see cref="Create"/> — DXGI swap chain creation, RTV heap allocation,
///     per-buffer view creation.</description></item>
/// <item><description><see cref="AcquireFrame"/> — return current back buffer + RTV CPU handle. Caller
///     uses these in the command list it builds for the frame.</description></item>
/// <item><description><see cref="Present"/> — submit the present, signal the fence, advance buffer
///     index, wait for the GPU to release the next buffer slot.</description></item>
/// <item><description><see cref="Resize"/> — drain GPU + rebuild back buffers + RTVs at the new size.</description></item>
/// </list>
///
/// Two-buffer flip-discard with optional tearing — minimum acceptable presentation
/// model for D3D12, scales up to triple buffering by changing <see cref="BufferCount"/>
/// at construction.
/// </summary>
public sealed unsafe class D3D12SwapChain : IDisposable
{
    public const int BufferCount = 2;

    private readonly D3D12RhiDevice _owningDevice;
    private IDXGISwapChain3* _swapChain;
    private ID3D12DescriptorHeap* _rtvHeap;
    private readonly ID3D12Resource*[] _backBuffers = new ID3D12Resource*[BufferCount];
    private readonly ulong[] _frameFenceValues = new ulong[BufferCount];
    private uint _rtvDescriptorSize;
    private ID3D12Fence* _fence;
    private nint _fenceEvent;
    private ulong _nextFenceValue = 1;
    private uint _backBufferIndex;
    private int _width;
    private int _height;
    private bool _disposed;

    private D3D12SwapChain(D3D12RhiDevice owningDevice, int width, int height)
    {
        _owningDevice = owningDevice;
        _width = width;
        _height = height;
    }

    public int Width => _width;

    public int Height => _height;

    public Format Format => Format.FormatR8G8B8A8Unorm;

    public uint CurrentBackBufferIndex => _backBufferIndex;

    public ID3D12Resource* CurrentBackBuffer => _backBuffers[_backBufferIndex];

    public CpuDescriptorHandle CurrentRenderTargetView
    {
        get
        {
            var handle = _rtvHeap->GetCPUDescriptorHandleForHeapStart();
            handle.Ptr += (nuint)(_backBufferIndex * _rtvDescriptorSize);
            return handle;
        }
    }

    /// <summary>
    /// Creates a flip-discard swap chain on the given window. Throws when the underlying
    /// COM call fails — caller is responsible for surfacing the error to the user.
    /// </summary>
    public static D3D12SwapChain Create(D3D12RhiDevice device, IntPtr hwnd, int width, int height)
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        if (hwnd == IntPtr.Zero)
        {
            throw new ArgumentException("hwnd must be non-zero", nameof(hwnd));
        }

        var instance = new D3D12SwapChain(device, width, height);
        instance.Initialise(hwnd);
        return instance;
    }

    /// <summary>
    /// Submits the present + signals the fence + waits for the GPU before handing the
    /// caller back the next back buffer. <paramref name="syncInterval"/> 0 = no vsync,
    /// 1 = vsync-aligned (every refresh), N = every Nth refresh.
    /// </summary>
    public void Present(uint syncInterval = 1)
    {
        SilkMarshal.ThrowHResult(_swapChain->Present(syncInterval, 0u));

        // Signal the queue with the next monotonic fence value, then advance the index.
        var queue = _owningDevice.GraphicsQueue;
        var signaledValue = _nextFenceValue;
        SilkMarshal.ThrowHResult(queue->Signal(_fence, signaledValue));
        _frameFenceValues[_backBufferIndex] = signaledValue;
        _nextFenceValue++;

        _backBufferIndex = _swapChain->GetCurrentBackBufferIndex();

        // Block until the GPU has finished the frame that previously used this buffer slot.
        // First time through every slot, _frameFenceValues[i] is 0 — fence already at 0.
        var awaited = _frameFenceValues[_backBufferIndex];
        if (_fence->GetCompletedValue() < awaited)
        {
            SilkMarshal.ThrowHResult(_fence->SetEventOnCompletion(awaited, (void*)_fenceEvent));
            // Bounded wait so a TDR'd GPU surfaces as a managed exception instead of an
            // infinite block (Windows otherwise marks the host window "Не отвечает" because
            // the SDL message pump stalls behind the wait). 5s is well past any honest
            // per-frame work — anything longer is a stuck driver.
            var waitResult = WaitForSingleObject(_fenceEvent, 5000u);
            if (waitResult != 0u)
            {
                var removed = _owningDevice.NativeDevice->GetDeviceRemovedReason();
                throw new EngineDeviceLostException(
                    $"D3D12 fence wait timed out after 5s on back-buffer slot {_backBufferIndex} (awaited={awaited}, completed={_fence->GetCompletedValue()}, WaitForSingleObject={waitResult:X8}, deviceRemovedReason=0x{removed:X8}). GPU is likely in TDR.",
                    removed);
            }
        }
    }

    /// <summary>Drains the GPU + rebuilds back buffers at the new size. Must run on the
    /// thread that owns the device. Caller is responsible for not having recorded
    /// commands referencing the old back buffers.</summary>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == _width && height == _height))
        {
            return;
        }

        _owningDevice.WaitForIdle();

        for (var i = 0; i < BufferCount; i++)
        {
            if (_backBuffers[i] != null)
            {
                _backBuffers[i]->Release();
                _backBuffers[i] = null;
            }
        }

        SilkMarshal.ThrowHResult(_swapChain->ResizeBuffers(
            BufferCount,
            (uint)width,
            (uint)height,
            Format,
            (uint)SwapChainFlag.AllowTearing));

        _width = width;
        _height = height;
        _backBufferIndex = _swapChain->GetCurrentBackBufferIndex();
        CreateRenderTargetViews();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Wait for GPU to finish anything in flight before tearing resources.
        try
        {
            _owningDevice.WaitForIdle();
        }
        catch
        {
            // Best-effort — we're tearing down anyway.
        }

        for (var i = 0; i < BufferCount; i++)
        {
            if (_backBuffers[i] != null)
            {
                _backBuffers[i]->Release();
                _backBuffers[i] = null;
            }
        }

        if (_rtvHeap != null)
        {
            _rtvHeap->Release();
            _rtvHeap = null;
        }

        if (_fence != null)
        {
            _fence->Release();
            _fence = null;
        }

        if (_swapChain != null)
        {
            _swapChain->Release();
            _swapChain = null;
        }

        if (_fenceEvent != 0)
        {
            CloseHandle(_fenceEvent);
            _fenceEvent = 0;
        }

        _disposed = true;
    }

    private void Initialise(IntPtr hwnd)
    {
        var device = _owningDevice.NativeDevice;
        var queue = _owningDevice.GraphicsQueue;
        var factory = _owningDevice.DxgiFactory;

        // Swap chain.
        var desc = new SwapChainDesc1
        {
            Width = (uint)_width,
            Height = (uint)_height,
            Format = Format,
            Stereo = 0,
            SampleDesc = new SampleDesc(1, 0),
            BufferUsage = DXGI.UsageRenderTargetOutput,
            BufferCount = BufferCount,
            Scaling = Scaling.None,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Unspecified,
            Flags = (uint)SwapChainFlag.AllowTearing,
        };

        IDXGISwapChain1* baseChain = null;
        SilkMarshal.ThrowHResult(factory->CreateSwapChainForHwnd(
            (IUnknown*)queue,
            hwnd,
            &desc,
            pFullscreenDesc: null,
            pRestrictToOutput: null,
            &baseChain));

        try
        {
            var sc3Guid = IDXGISwapChain3.Guid;
            IDXGISwapChain3* sc3 = null;
            SilkMarshal.ThrowHResult(baseChain->QueryInterface(&sc3Guid, (void**)&sc3));
            _swapChain = sc3;
        }
        finally
        {
            baseChain->Release();
        }

        // Disable Alt-Enter fullscreen toggle — production engines own the fullscreen
        // mode switch through their own settings UI, not the OS shortcut.
        SilkMarshal.ThrowHResult(factory->MakeWindowAssociation(hwnd, 1u << 1));

        _backBufferIndex = _swapChain->GetCurrentBackBufferIndex();

        // RTV descriptor heap — one slot per back buffer.
        var rtvHeapDesc = new DescriptorHeapDesc
        {
            Type = DescriptorHeapType.Rtv,
            NumDescriptors = BufferCount,
            Flags = DescriptorHeapFlags.None,
            NodeMask = 0,
        };
        var rtvHeapGuid = ID3D12DescriptorHeap.Guid;
        ID3D12DescriptorHeap* rtvHeap = null;
        SilkMarshal.ThrowHResult(device->CreateDescriptorHeap(&rtvHeapDesc, &rtvHeapGuid, (void**)&rtvHeap));
        _rtvHeap = rtvHeap;
        _rtvDescriptorSize = device->GetDescriptorHandleIncrementSize(DescriptorHeapType.Rtv);

        CreateRenderTargetViews();

        // Fence + event.
        var fenceGuid = ID3D12Fence.Guid;
        ID3D12Fence* fence = null;
        SilkMarshal.ThrowHResult(device->CreateFence(0, FenceFlags.None, &fenceGuid, (void**)&fence));
        _fence = fence;

        _fenceEvent = CreateEventW(IntPtr.Zero, false, false, null);
        if (_fenceEvent == 0)
        {
            throw new InvalidOperationException("CreateEventW returned null event handle.");
        }
    }

    private void CreateRenderTargetViews()
    {
        var device = _owningDevice.NativeDevice;
        var handle = _rtvHeap->GetCPUDescriptorHandleForHeapStart();

        for (uint i = 0; i < BufferCount; i++)
        {
            var bufferGuid = ID3D12Resource.Guid;
            ID3D12Resource* buffer = null;
            SilkMarshal.ThrowHResult(_swapChain->GetBuffer(i, &bufferGuid, (void**)&buffer));
            _backBuffers[i] = buffer;

            device->CreateRenderTargetView(buffer, pDesc: null, handle);
            handle.Ptr += (nuint)_rtvDescriptorSize;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateEventW(IntPtr lpEventAttributes, [MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool bManualReset, [MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool bInitialState, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);
}
