using System;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Live Direct3D 12 backend for <see cref="IRhiDevice"/>. Owns the DXGI factory,
/// adapter, device, graphics command queue, optional debug messenger, and a fence
/// for GPU-idle waits. Resource / view / command-list / shader / pipeline factory
/// methods live in partial files alongside this one.
/// </summary>
public sealed unsafe partial class D3D12RhiDevice : IRhiDevice
{
    private readonly DXGI _dxgiApi;
    private readonly D3D12 _d3d12Api;
    private IDXGIFactory6* _factory;
    private IDXGIAdapter1* _adapter;
    private ID3D12Device* _device;
    private ID3D12CommandQueue* _graphicsQueue;
    private D3D12DebugMessenger? _debugMessenger;
    private Win32FenceWait? _idleWait;
    private bool _disposed;

    private D3D12RhiDevice(
        DXGI dxgiApi,
        D3D12 d3d12Api,
        IDXGIFactory6* factory,
        IDXGIAdapter1* adapter,
        ID3D12Device* device,
        ID3D12CommandQueue* graphicsQueue,
        AdapterInfo adapterInfo,
        RhiCapabilities capabilities,
        D3D12DebugMessenger? debugMessenger)
    {
        _dxgiApi = dxgiApi;
        _d3d12Api = d3d12Api;
        _factory = factory;
        _adapter = adapter;
        _device = device;
        _graphicsQueue = graphicsQueue;
        _debugMessenger = debugMessenger;
        AdapterInfo = adapterInfo;
        Capabilities = capabilities;
    }

    public RhiBackendKind Backend => RhiBackendKind.D3D12;

    public RhiCapabilities Capabilities { get; }

    public string AdapterName => AdapterInfo.Description;

    public AdapterInfo AdapterInfo { get; }

    public ID3D12Device* NativeDevice => _device;

    public ID3D12CommandQueue* GraphicsQueue => _graphicsQueue;

    public IDXGIFactory6* DxgiFactory => _factory;

    public D3D12DebugMessenger? DebugMessenger => _debugMessenger;

    /// <summary>Tries to create a D3D12 device on the highest-priority adapter. Returns null
    /// on non-Windows hosts, when no D3D12-capable adapter is present, or when device creation
    /// fails. Callers wanting hard failure null-check + throw themselves.</summary>
    public static D3D12RhiDevice? TryCreate(bool enableDebugLayer = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        DXGI? dxgi = null;
        D3D12? d3d12 = null;
        IDXGIFactory6* factory = null;
        IDXGIAdapter1* adapter = null;
        ID3D12Device* device = null;
        ID3D12CommandQueue* graphicsQueue = null;

        try
        {
            dxgi = DXGI.GetApi();
            d3d12 = D3D12.GetApi();

            if (enableDebugLayer)
            {
                D3D12DebugLayer.Enable(d3d12);
            }

            var factoryGuid = IDXGIFactory6.Guid;
            if (dxgi.CreateDXGIFactory2(0u, &factoryGuid, (void**)&factory) < 0)
            {
                return null;
            }

            adapter = AdapterEnumeration.PickHighestPriority(factory);
            if (adapter == null)
            {
                return null;
            }

            AdapterDesc1 desc;
            adapter->GetDesc1(&desc);
            var adapterInfo = AdapterEnumeration.DescriptionToInfo(0, in desc);

            var deviceGuid = ID3D12Device.Guid;
            if (d3d12.CreateDevice((IUnknown*)adapter, D3DFeatureLevel.Level120, &deviceGuid, (void**)&device) < 0)
            {
                return null;
            }

            graphicsQueue = D3D12CommandQueueFactory.CreateGraphics(device);
            if (graphicsQueue == null)
            {
                return null;
            }

            var capabilities = D3D12CapabilityProbe.Query(device);
            var debugMessenger = enableDebugLayer ? D3D12DebugMessenger.TryAttach(device) : null;

            var instance = new D3D12RhiDevice(
                dxgi, d3d12, factory, adapter, device, graphicsQueue, adapterInfo, capabilities, debugMessenger);

            // Ownership transferred — null the locals so the finally block doesn't release them.
            factory = null;
            adapter = null;
            device = null;
            graphicsQueue = null;
            dxgi = null;
            d3d12 = null;
            return instance;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (graphicsQueue != null)
            {
                graphicsQueue->Release();
            }

            if (device != null)
            {
                device->Release();
            }

            if (adapter != null)
            {
                adapter->Release();
            }

            if (factory != null)
            {
                factory->Release();
            }

            d3d12?.Dispose();
            dxgi?.Dispose();
        }
    }

    /// <summary>Drains the graphics queue: signals a monotonic fence then parks the CPU
    /// until the GPU reaches that signal. Required before destroying any GPU-referenced
    /// object (root sig, PSO, resource, command allocator).</summary>
    public void WaitForIdle()
    {
        if (_disposed || _device == null || _graphicsQueue == null)
        {
            return;
        }

        _idleWait ??= new Win32FenceWait(_device);
        _idleWait.Drain(_graphicsQueue);
    }

    public IRhiShader CreateShader(RhiShaderDescription description) =>
        throw new NotImplementedException("Shader creation lands in R-1.4 (HLSL → DXIL pipeline).");

    public IRhiPipeline CreatePipeline(RhiPipelineDescription description) =>
        throw new NotImplementedException("Pipeline creation lands in R-1.4.");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            WaitForIdle();
        }
        catch
        {
            // Best-effort drain before teardown.
        }

        _idleWait?.Dispose();
        _idleWait = null;

        if (_debugMessenger != null)
        {
            _debugMessenger.Dispose();
            _debugMessenger = null;
        }

        if (_graphicsQueue != null)
        {
            _graphicsQueue->Release();
            _graphicsQueue = null;
        }

        if (_device != null)
        {
            _device->Release();
            _device = null;
        }

        if (_adapter != null)
        {
            _adapter->Release();
            _adapter = null;
        }

        if (_factory != null)
        {
            _factory->Release();
            _factory = null;
        }

        // Don't Dispose the D3D12 / DXGI APIs — that unloads d3d12.dll / dxgi.dll. Process-wide
        // load/unload churn (xUnit creates a new device per D3D12 integration test) exhausts
        // Windows loader resources; testhost eventually hangs on a subsequent SDL_Init or
        // D3D12CreateDevice with the loader lock held.
        _disposed = true;
    }
}
