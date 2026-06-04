using System;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>RAII holder for a native <c>ID3D12PipelineState*</c>.</summary>
public sealed unsafe class D3D12GraphicsPipeline : IDisposable
{
    private ID3D12PipelineState* _pso;
    private bool _disposed;

    internal D3D12GraphicsPipeline(ID3D12PipelineState* pso)
    {
        _pso = pso;
    }

    public ID3D12PipelineState* Native => _pso;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_pso != null)
        {
            _pso->Release();
            _pso = null;
        }

        _disposed = true;
    }
}
