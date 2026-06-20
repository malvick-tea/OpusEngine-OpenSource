using System;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>RAII holder for a native <c>ID3D12RootSignature*</c>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "MA0055:Do not use finalizer",
    Justification = "The finalizer releases only one unmanaged COM reference.")]
public sealed unsafe class D3D12RootSignature : IDisposable
{
    private ID3D12RootSignature* _rootSignature;
    private bool _disposed;

    internal D3D12RootSignature(ID3D12RootSignature* rootSignature)
    {
        _rootSignature = rootSignature;
    }

    public ID3D12RootSignature* Native => _rootSignature;

    ~D3D12RootSignature()
    {
        ReleaseNative();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseNative();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ReleaseNative()
    {
        if (_rootSignature != null)
        {
            _rootSignature->Release();
            _rootSignature = null;
        }
    }
}
